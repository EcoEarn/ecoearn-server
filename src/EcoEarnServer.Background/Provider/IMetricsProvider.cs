using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.TokenStaking.Provider;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface IMetricsProvider
{
    public Task<string> BatchGetBalanceAsync(string address, List<string> symbol, string chainId);
    public Task<string> GetBalanceAsync(string address, string symbol, string chainId);
}

public class MetricsProvider : AbpRedisCache, IMetricsProvider, ISingletonDependency
{
    private const string BatchBalanceRedisKeyPrefix = "EcoEarnServer:BatchBalance:";
    private const string BalanceRedisKeyPrefix = "EcoEarnServer:Balance:";


    private readonly ILogger<MetricsProvider> _logger;
    private readonly IDistributedCacheSerializer _serializer;
    private readonly IContractProvider _contractProvider;
    private readonly IPriceProvider _priceProvider;

    public MetricsProvider(IOptions<RedisCacheOptions> optionsAccessor, ILogger<MetricsProvider> logger,
        IDistributedCacheSerializer serializer, IContractProvider contractProvider,
        IPriceProvider priceProvider) : base(
        optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
        _contractProvider = contractProvider;
        _priceProvider = priceProvider;
    }

    public async Task<string> BatchGetBalanceAsync(string address, List<string> symbols, string chainId)
    {
        await ConnectAsync();
        var redisValue = await RedisDatabase.StringGetAsync(BatchBalanceRedisKeyPrefix + address);
        if (redisValue.HasValue)
        {
            _logger.LogInformation("get balance: {rewards}", redisValue);
            return _serializer.Deserialize<string>(redisValue);
        }

        var balance = decimal.Zero;
        foreach (var symbol in symbols)
        {
            var input = new GetBalanceInput
            {
                Symbol = symbol,
                Owner = Address.FromBase58(address)
            };
            try
            {
                var transaction = _contractProvider
                    .CreateTransaction(chainId, ContractConstants.SenderName, ContractConstants.TokenContractName,
                        ContractConstants.GetBalance, input)
                    .Result
                    .transaction;
                var transactionResult =
                    await _contractProvider.CallTransactionAsync<BalanceDto>(chainId, transaction);
                var rate = await _priceProvider.GetGateIoPriceAsync($"{symbol.ToUpper()}_USDT");
                balance += decimal.Parse(transactionResult.Balance) *
                           decimal.Parse(rate.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                _logger.LogError("get balance fail.{symbol}", symbol);
            }
        }

        await RedisDatabase.StringSetAsync(BatchBalanceRedisKeyPrefix + address,
            _serializer.Serialize(balance.ToString(CultureInfo.InvariantCulture)), TimeSpan.FromMinutes(1));
        return balance.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<string> GetBalanceAsync(string address, string symbol, string chainId)
    {
        await ConnectAsync();
        var redisValue = await RedisDatabase.StringGetAsync(BalanceRedisKeyPrefix + symbol + ":" + address);
        if (redisValue.HasValue)
        {
            _logger.LogInformation("get balance: {rewards}", redisValue);
            return _serializer.Deserialize<string>(redisValue);
        }

        var input = new GetBalanceInput
        {
            Symbol = symbol,
            Owner = Address.FromBase58(address)
        };
        try
        {
            var transaction = _contractProvider
                .CreateTransaction(chainId, ContractConstants.SenderName, ContractConstants.TokenContractName,
                    ContractConstants.GetBalance, input)
                .Result
                .transaction;
            var transactionResult =
                await _contractProvider.CallTransactionAsync<BalanceDto>(chainId, transaction);
            await RedisDatabase.StringSetAsync(BalanceRedisKeyPrefix + symbol + ":" + address,
                _serializer.Serialize(transactionResult.Balance), TimeSpan.FromMinutes(5));
            return transactionResult.Balance;
        }
        catch (Exception e)
        {
            _logger.LogError("get balance fail.{symbol}", symbol);
            return "0";
        }
    }
}