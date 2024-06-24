using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using EcoEarnServer.Common;
using EcoEarnServer.Common.Dtos;
using EcoEarnServer.Common.HttpClient;
using EcoEarnServer.Options;
using Io.Gate.GateApi.Api;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.TokenStaking.Provider;

public interface IPriceProvider
{
    Task<double> GetGateIoPriceAsync(string currencyPair);
    Task<double> GetLpPriceAsync(string stakingToken, double feeRate, string symbol0 = "", string symbol1 = "");
}

public class PriceProvider : AbpRedisCache, IPriceProvider, ISingletonDependency
{
    private readonly ILogger<PriceProvider> _logger;
    private readonly IDistributedCacheSerializer _serializer;
    private readonly IHttpProvider _httpProvider;
    private readonly LpPoolRateOptions _lpPoolRateOptions;

    public PriceProvider(IOptions<RedisCacheOptions> optionsAccessor, ILogger<PriceProvider> logger,
        IDistributedCacheSerializer serializer, IHttpProvider httpProvider,
        IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions) : base(
        optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
        _httpProvider = httpProvider;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
    }

    public async Task<double> GetGateIoPriceAsync(string currencyPair)
    {
        _logger.LogInformation("[PriceDataProvider][GateIo] Start.");
        await ConnectAsync();
        var redisValue = await RedisDatabase.StringGetAsync(currencyPair);
        if (redisValue.HasValue)
        {
            _logger.LogInformation("get price cache: {redisValue}", redisValue);
            return _serializer.Deserialize<double>(redisValue);
        }

        double price = 0;
        try
        {
            var spotApi = new SpotApi();
            var tickers = await spotApi.ListTickersAsync(currencyPair);
            if (!tickers.IsNullOrEmpty())
            {
                var last = tickers[0].Last;
                price = double.Parse(last);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PriceDataProvider][GateIo] Parse response error.");
        }

        await RedisDatabase.StringSetAsync(currencyPair, _serializer.Serialize(price), TimeSpan.FromMinutes(2));
        return price;
    }

    public async Task<double> GetLpPriceAsync(string stakingToken, double feeRate, string symbol0 = "", string symbol1 = "")
    {
        try
        {
            _logger.LogInformation("[PriceDataProvider][GetLpPriceAsync] Start.");
            if (string.IsNullOrEmpty(symbol0) || string.IsNullOrEmpty(symbol1))
            {
                (symbol0, symbol1) = GetLpSymbols(stakingToken);
            }
            if (string.IsNullOrEmpty(symbol0) || string.IsNullOrEmpty(symbol1))
            {
                return 0;
            }

            var key = GuidHelper.GenerateId(symbol0, symbol1, feeRate.ToString(CultureInfo.InvariantCulture));
            await ConnectAsync();
            var redisValue = await RedisDatabase.StringGetAsync(key);
            if (redisValue.HasValue)
            {
                _logger.LogInformation("get lp price cache: {redisValue}", redisValue);
                return _serializer.Deserialize<double>(redisValue);
            }

            var resp = await _httpProvider.InvokeAsync<CommonResponseDto<LpPriceDto>>(HttpMethod.Get,
                _lpPoolRateOptions.LpPriceServer.LpPriceServerBaseUrl,
                param: new Dictionary<string, string>
                {
                    ["token0Symbol"] = symbol0,
                    ["token1Symbol"] = symbol1,
                    ["feeRate"] = feeRate.ToString(CultureInfo.InvariantCulture),
                    ["chainId"] = _lpPoolRateOptions.LpPriceServer.ChainId,
                }, header: null);
            double price = 0;
            if (resp.Success && resp.Data != null && !resp.Data.Items.IsNullOrEmpty())
            {
                var itemDto = resp.Data.Items[0];
                price = itemDto.Tvl / double.Parse(itemDto.TotalSupply);
            }

            await RedisDatabase.StringSetAsync(key, _serializer.Serialize(price), TimeSpan.FromMinutes(2));
            return price;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PriceDataProvider][GetLpPriceAsync] Parse response error.");
            return 0;
        }
    }

    private (string, string) GetLpSymbols(string stakingToken)
    {
        if (string.IsNullOrEmpty(stakingToken))
        {
            return ("", "");
        }

        var split = stakingToken.Split("ALP ");
        if (split.Length != 2)
        {
            return ("", "");
        }

        var symbols = split[1].Split("-");
        if (symbols.Length != 2)
        {
            return ("", "");
        }

        return (symbols[0], symbols[1]);
    }
}