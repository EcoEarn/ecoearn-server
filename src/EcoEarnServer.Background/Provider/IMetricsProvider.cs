using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using EcoEarn.Contracts.Tokens;
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
    public Task<string> GetBalanceAsync(string address, string symbol, string chainId, string contractAddress);
    public Task<PoolAddressInfoDto> GetAddressInfoAsync(string poolId, string chainId);
}

public class MetricsProvider : AbpRedisCache, IMetricsProvider, ISingletonDependency
{
    private const string BatchBalanceRedisKeyPrefix = "EcoEarnServer:BatchBalance:";
    private const string BalanceRedisKeyPrefix = "EcoEarnServer:Balance:";
    private const string PoolAddressRedisKeyPrefix = "EcoEarnServer:PoolAddressInfo:";


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

    public async Task<string> GetBalanceAsync(string address, string symbol, string chainId, string contractAddress)
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
                .CreateTransactionByContract(chainId, ContractConstants.SenderName, contractAddress,
                    ContractConstants.GetBalance, input)
                .Result
                .transaction;
            var transactionResult =
                await _contractProvider.CallTransactionAsync<BalanceDto>(chainId, transaction);
            var balance = (decimal.Parse(transactionResult.Balance) + decimal.Parse(transactionResult.Amount)).ToString(CultureInfo.InvariantCulture);
            await RedisDatabase.StringSetAsync(BalanceRedisKeyPrefix + symbol + ":" + address,
                _serializer.Serialize(balance), TimeSpan.FromMinutes(5));
            return balance;
        }
        catch (Exception e)
        {
            _logger.LogError("get balance fail.{symbol}", symbol);
            return "0";
        }
    }

    public async Task<PoolAddressInfoDto> GetAddressInfoAsync(string poolId, string chainId)
    {
        await ConnectAsync();
        var redisValue = await RedisDatabase.StringGetAsync(PoolAddressRedisKeyPrefix + poolId);
        if (redisValue.HasValue)
        {
            _logger.LogInformation("get pool address info: {info}", redisValue);
            return _serializer.Deserialize<PoolAddressInfoDto>(redisValue);
        }

        try
        {
            var transaction = _contractProvider
                .CreateTransaction(chainId, ContractConstants.SenderName, ContractConstants.ContractName,
                    ContractConstants.GetPoolAddressInfo, Hash.LoadFromHex(poolId))
                .Result
                .transaction;
            var transactionResult =
                await _contractProvider.CallTransactionAsync<PoolAddressInfo>(chainId, transaction);
            var addressInfoDto = new PoolAddressInfoDto
            {
                StakeAddress = transactionResult.StakeAddress.ToBase58(),
                RewardAddress = transactionResult.RewardAddress.ToBase58(),
            };
            await RedisDatabase.StringSetAsync(PoolAddressRedisKeyPrefix + poolId,
                _serializer.Serialize(addressInfoDto), TimeSpan.FromDays(2));
            return addressInfoDto;
        }
        catch (Exception e)
        {
            _logger.LogError("get pool address info.{poolId}", poolId);
            return null;
        }
    }
}