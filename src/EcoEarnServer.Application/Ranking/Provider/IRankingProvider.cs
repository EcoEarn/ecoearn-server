using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using AElf.Types;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.ExceptionHandle;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Ranking.Provider;

public interface IRankingProvider
{
    Task<bool> JoinCheckAsync(string chainId, string address);
    Task<(long, List<PointsRankingIndex>)> GetPointsRankingListAsync(int skipCount, int maxResultCount);
    Task<PointsRankingIndex> GetOwnerRankingPointsAsync(string address);
}

public class RankingProvider : AbpRedisCache, IRankingProvider, ISingletonDependency
{
    private const string JoinCheckStatusRedisKeyPrefix = "EcoEarnServer:JoinCheckStatus:";

    private readonly INESTRepository<PointsRankingIndex, string> _repository;
    private readonly IContractProvider _contractProvider;
    private readonly IDistributedCacheSerializer _serializer;
    private readonly ILogger<RankingProvider> _logger;

    public RankingProvider(IContractProvider contractProvider, IOptions<RedisCacheOptions> optionsAccessor,
        IDistributedCacheSerializer serializer, ILogger<RankingProvider> logger,
        INESTRepository<PointsRankingIndex, string> repository) : base(
        optionsAccessor)
    {
        _contractProvider = contractProvider;
        _serializer = serializer;
        _logger = logger;
        _repository = repository;
    }

    public async Task<bool> JoinCheckAsync(string chainId, string address)
    {
        await ConnectAsync();
        var redisValue = await RedisDatabase.StringGetAsync(JoinCheckStatusRedisKeyPrefix + address);
        if (redisValue.HasValue)
        {
            return _serializer.Deserialize<bool>(redisValue);
        }

        var isJoin = await GetJoinRecordAsync(chainId, address);
        if (isJoin)
        {
            await RedisDatabase.StringSetAsync(JoinCheckStatusRedisKeyPrefix + address,
                _serializer.Serialize(true), TimeSpan.FromDays(30));
        }

        return isJoin;
    }

    public async Task<(long, List<PointsRankingIndex>)> GetPointsRankingListAsync(int skipCount, int maxResultCount)
    {
        var (total, list) = await _repository.GetListAsync(skip: skipCount, limit: maxResultCount,
            sortType: SortOrder.Descending, sortExp: o => o.Points);
        return (total, list);
    }

    public async Task<PointsRankingIndex> GetOwnerRankingPointsAsync(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return null;
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<PointsRankingIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Terms(i => i.Field(f => f.Address).Terms(address)));

        QueryContainer Filter(QueryContainerDescriptor<PointsRankingIndex> f) => f.Bool(b => b.Must(mustQuery));
        return await _repository.GetAsync(Filter);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService),
        ReturnDefault = ReturnDefault.Default, MethodName = nameof(ExceptionHandlingService.HandleException),
        Message = "GetJoinRecord Indexer error")]
    private async Task<bool> GetJoinRecordAsync(string chainId, string address)
    {
        var transaction = _contractProvider
            .CreateTransaction(chainId, ContractConstants.SenderName, ContractConstants.RewardsContractName,
                ContractConstants.GetJoinRecord, Address.FromBase58(address))
            .Result
            .transaction;
        var transactionOutput = await _contractProvider.CallTransactionAsync(chainId, transaction);
        return bool.Parse(transactionOutput);
    }
}