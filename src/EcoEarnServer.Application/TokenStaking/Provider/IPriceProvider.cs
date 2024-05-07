using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Io.Gate.GateApi.Api;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.TokenStaking.Provider;

public interface IPriceProvider
{
    Task<double> GetGateIoPriceAsync(string currencyPair);
}

public class PriceProvider : IPriceProvider, ISingletonDependency
{
    private readonly ILogger<PriceProvider> _logger;

    public PriceProvider(ILogger<PriceProvider> logger)
    {
        _logger = logger;
    }

    public async Task<double> GetGateIoPriceAsync(string currencyPair)
    {
        try
        {
            _logger.LogInformation("[PriceDataProvider][GateIo] Start.");
            var spotApi = new SpotApi();
            var tickers = await spotApi.ListTickersAsync(currencyPair);
            if (tickers.IsNullOrEmpty())
            {
                return 0;
            }

            var last = tickers[0].Last;
            return double.Parse(last);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PriceDataProvider][GateIo] Parse response error.");
            return 0;
        }
    }
}