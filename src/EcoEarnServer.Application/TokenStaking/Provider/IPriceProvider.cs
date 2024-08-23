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
    private readonly HamsterServerOptions _hamsterServerOptions;

    public PriceProvider(IOptions<RedisCacheOptions> optionsAccessor, ILogger<PriceProvider> logger,
        IDistributedCacheSerializer serializer, IHttpProvider httpProvider,
        IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions,
        IOptionsSnapshot<HamsterServerOptions> hamsterServerOptions) : base(
        optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
        _httpProvider = httpProvider;
        _hamsterServerOptions = hamsterServerOptions.Value;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
    }

    public async Task<double> GetGateIoPriceAsync(string pair)
    {
        var currencyPair = pair;
        var split = pair.Split("_");
        if (split.Length == 2 && _lpPoolRateOptions.SymbolMappingsDic.TryGetValue(split[0], out var mappingSymbol))
        {
            currencyPair = mappingSymbol + "_" + split[1];
        }

        await ConnectAsync();
        var redisValue = await RedisDatabase.StringGetAsync(currencyPair);
        if (redisValue.HasValue)
        {
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
            _logger.LogWarning("[PriceDataProvider][GateIo] Parse response error.");
            if (currencyPair.ToUpper().Contains(_hamsterServerOptions.Symbol))
            {
                price = await GetHamsterSymbolUsdPriceAsync();
            }
        }

        await RedisDatabase.StringSetAsync(currencyPair, _serializer.Serialize(price), TimeSpan.FromMinutes(2));
        return price;
    }

    public async Task<double> GetLpPriceAsync(string stakingToken, double feeRate, string symbol0 = "",
        string symbol1 = "")
    {
        try
        {
            if (string.IsNullOrEmpty(symbol0) || string.IsNullOrEmpty(symbol1))
            {
                (symbol0, symbol1) = LpSymbolHelper.GetLpSymbols(stakingToken);
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
                return _serializer.Deserialize<double>(redisValue);
            }

            var rate = feeRate.ToString(CultureInfo.InvariantCulture);
            var resp = await _httpProvider.InvokeAsync<CommonResponseDto<LpPriceDto>>(HttpMethod.Get,
                _lpPoolRateOptions.LpPriceServer.LpPriceServerBaseUrl,
                param: new Dictionary<string, string>
                {
                    ["token0Symbol"] = symbol0,
                    ["token1Symbol"] = symbol1,
                    ["feeRate"] = rate,
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
            _logger.LogError("[PriceDataProvider][GetLpPriceAsync] Parse response error.");
            return 0;
        }
    }

    private async Task<double> GetHamsterSymbolUsdPriceAsync()
    {
        var apiInfo = new ApiInfo(HttpMethod.Get, "api/app/hamster-pass/price");
        var usdPrice = 0d;
        try
        {
            var resp = await _httpProvider.InvokeAsync<CommonResponseDto<HamsterPriceDataDto>>(
                _hamsterServerOptions.BaseUrl, apiInfo);
            usdPrice = resp.Data.AcornsInUsd;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get hamster symbol usd price fail.");
        }

        return usdPrice;
    }
}