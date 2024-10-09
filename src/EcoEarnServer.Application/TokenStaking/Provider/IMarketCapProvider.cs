using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using EcoEarnServer.Common.HttpClient;
using EcoEarnServer.Options;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.TokenStaking.Provider;

public interface IMarketCapProvider
{
    Task<decimal> GetSymbolMarketCapAsync(string symbol);
}

public class MarketCapProvider : AbpRedisCache, IMarketCapProvider, ISingletonDependency
{
    private const string MarketCapRedisKeyPrefix = "EcoEarnServer:MarketCap:";

    private readonly ILogger<MarketCapProvider> _logger;
    private readonly IDistributedCacheSerializer _serializer;
    private readonly IHttpProvider _httpProvider;
    private readonly CoinMarketCapServerOptions _capServerOptions;
    private readonly LpPoolRateOptions _lpPoolRateOptions;
    private readonly IPriceProvider _priceProvider;

    public MarketCapProvider(IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsSnapshot<CoinMarketCapServerOptions> capServerOptions,
        ILogger<MarketCapProvider> logger, IDistributedCacheSerializer serializer, IHttpProvider httpProvider,
        IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions, IPriceProvider priceProvider) : base(optionsAccessor)
    {
        _capServerOptions = capServerOptions.Value;
        _logger = logger;
        _serializer = serializer;
        _httpProvider = httpProvider;
        _priceProvider = priceProvider;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
    }

    public async Task<decimal> GetSymbolMarketCapAsync(string symbol)
    {
        await ConnectAsync();
        if (_lpPoolRateOptions.SymbolMappingsDic.TryGetValue(symbol, out var mappingSymbol))
        {
            symbol = mappingSymbol;
        }

        var redisValue = await RedisDatabase.StringGetAsync(MarketCapRedisKeyPrefix + symbol);
        if (redisValue.HasValue)
        {
            return _serializer.Deserialize<decimal>(redisValue);
        }

        var apiInfo = new ApiInfo(HttpMethod.Get, "/v2/cryptocurrency/quotes/latest");
        var param = new Dictionary<string, string> { { "symbol", symbol } };
        var header = new Dictionary<string, string> { { "X-CMC_PRO_API_KEY", _capServerOptions.ApiKey } };
        decimal marketCap = 0;
        var expired = 10;
        try
        {
            var resp = await _httpProvider.InvokeAsync<MarketCapDto>(
                _capServerOptions.BaseUrl, apiInfo, param: param, header: header);
            marketCap = resp.Data[symbol].Sum(x => x.Quote.USD.Market_Cap);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get market cap fail.");
            expired = 1;
        }

        await RedisDatabase.StringSetAsync(MarketCapRedisKeyPrefix + symbol, _serializer.Serialize(marketCap),
            TimeSpan.FromMinutes(expired));
        return marketCap;
    }
}