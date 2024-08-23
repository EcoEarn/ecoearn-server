using System;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface IMetricsProvider
{
    public Task<string> GetBalanceAsync(string address, string symbol, string chainId);
}

public class MetricsProvider : AbpRedisCache, IMetricsProvider, ISingletonDependency
{
    private const string BalanceRedisKeyPrefix = "EcoEarnServer:Balance:";


    private readonly ILogger<MetricsProvider> _logger;
    private readonly IDistributedCacheSerializer _serializer;
    private readonly IContractProvider _contractProvider;

    public MetricsProvider(IOptions<RedisCacheOptions> optionsAccessor, ILogger<MetricsProvider> logger,
        IDistributedCacheSerializer serializer, IContractProvider contractProvider) : base(
        optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
        _contractProvider = contractProvider;
    }

    public async Task<string> GetBalanceAsync(string address, string symbol, string chainId)
    {
        try
        {
            await ConnectAsync();
            var redisValue = await RedisDatabase.StringGetAsync(BalanceRedisKeyPrefix + address);
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
            var transaction = _contractProvider
                .CreateTransaction(chainId, ContractConstants.SenderName, ContractConstants.TokenContractName,
                    ContractConstants.GetBalance, input)
                .Result
                .transaction;
            var transactionResult =
                await _contractProvider.CallTransactionAsync<BalanceDto>(chainId, transaction);

            await RedisDatabase.StringSetAsync(BalanceRedisKeyPrefix + address,
                _serializer.Serialize(transactionResult.Balance), TimeSpan.FromSeconds(5));
            return transactionResult.Balance;
        }
        catch (Exception e)
        {
            _logger.LogError("get balance fail.");
            return "0";
        }
    }
}