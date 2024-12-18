using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.ExceptionHandle;
using EcoEarnServer.Common.HttpClient;
using EcoEarnServer.Options;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using GraphQL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Linq;
using Nest;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Rewards.Provider;

public interface IRewardsProvider
{
    Task<RewardsListIndexerResult> GetRewardsListAsync(PoolTypeEnums poolType, string address, int skipCount,
        int maxResultCount, bool filterWithdraw = false, bool filterUnlocked = false, List<string> liquidityIds = null,
        List<string> poolIds = null, List<string> dappIds = null);

    Task<List<string>> GetUnLockedStakeIdsAsync(List<string> stakeIds, string address);

    Task<List<RewardOperationRecordIndex>> GetRewardOperationRecordListAsync(string address,
        List<ExecuteStatus> executeStatus);

    Task<List<RewardOperationRecordIndex>> GetExecutingListAsync(string address, ExecuteType executeType);

    Task<long> GetRewardsCountAsync(PoolTypeEnums poolType, string address, int skipCount,
        int maxResultCount, bool filterWithdraw = false, bool filterUnlocked = false, List<string> liquidityIds = null);

    Task<MergedRewardsListIndexerResult> GetMergedRewardsListAsync(string address, List<string> poolIds,
        PoolTypeEnums poolType, List<string> dappIds = null, int skipCount = 0, int maxResultCount = 5000);

    Task<RewardsInfoListIndexerDto> GetRewardsInfoListAsync(PoolTypeEnums poolType, string address, string id,
        int skipCount,
        int maxResultCount);

    Task<long> GetConfirmBlockHeightAsync(string chainId);
}

public class RewardsProvider : IRewardsProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<RewardsProvider> _logger;
    private readonly INESTRepository<RewardOperationRecordIndex, string> _repository;
    private readonly IHttpProvider _httpProvider;
    private readonly IndexerSyncStateOptions _indexerSyncStateOptions;

    public RewardsProvider(IGraphQlHelper graphQlHelper, ILogger<RewardsProvider> logger,
        INESTRepository<RewardOperationRecordIndex, string> repository, IHttpProvider httpProvider, 
        IOptionsSnapshot<IndexerSyncStateOptions> indexerSyncStateOptions)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
        _repository = repository;
        _httpProvider = httpProvider;
        _indexerSyncStateOptions = indexerSyncStateOptions.Value;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), ReturnDefault = ReturnDefault.New,
        MethodName = nameof(ExceptionHandlingService.HandleException), Message = "GetClaimInfoList Indexer error")]
    public virtual async Task<RewardsListIndexerResult> GetRewardsListAsync(PoolTypeEnums poolType, string address,
        int skipCount, int maxResultCount, bool filterWithdraw = false, bool filterUnlocked = false,
        List<string> liquidityIds = null, List<string> poolIds = null, List<string> dappIds = null)
    {
        if (string.IsNullOrEmpty(address))
        {
            return new RewardsListIndexerResult();
        }

        var indexerResult = await _graphQlHelper.QueryAsync<RewardsListQuery>(new GraphQLRequest
        {
            Query =
                @"query($poolType:PoolType!, $liquidityIds:[String!]!, $poolIds:[String!]!, $dappIds:[String!]!,$filterUnlock:Boolean!,$filterWithdraw:Boolean!,$address:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getClaimInfoList(input: {poolType:$poolType,liquidityIds:$liquidityIds,poolIds:$poolIds,dappIds:$dappIds,filterUnlock:$filterUnlock,filterWithdraw:$filterWithdraw,address:$address,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        claimId,
                        poolId,
                        claimedAmount,
                        seed,
                        claimedSymbol,
                        claimedBlockNumber,
                        earlyStakedAmount,
    					claimedTime,
    					releaseTime,
    					withdrawTime,
    					earlyStakeTime,
    					account,
    					poolType,
    					lockState,
    					withdrawSeed,
    					contractAddress,
                        liquidityAddedInfos{liquidityAddedSeed,liquidityId,tokenALossAmount,tokenBLossAmount,addedTime},
                        earlyStakeInfos{earlyStakeSeed,stakeId,stakeTime},
                    }
                }
            }",
            Variables = new
            {
                poolType = poolType, filterUnlock = filterUnlocked, filterWithdraw = filterWithdraw,
                address = address, skipCount = skipCount, maxResultCount = maxResultCount,
                liquidityIds = liquidityIds ?? new List<string>(),
                poolIds = poolIds ?? new List<string>(),
                dappIds = dappIds ?? new List<string>()
            }
        });

        return indexerResult.GetClaimInfoList;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), ReturnDefault = ReturnDefault.New,
        MethodName = nameof(ExceptionHandlingService.HandleException), Message = "GetUnLockedStakeIds Indexer error")]
    public virtual async Task<List<string>> GetUnLockedStakeIdsAsync(List<string> stakeIds, string address)
    {
        if (string.IsNullOrEmpty(address) || stakeIds.IsNullOrEmpty())
        {
            return new List<string>();
        }

        var indexerResult = await _graphQlHelper.QueryAsync<UnLockedStakeIdsQuery>(new GraphQLRequest
        {
            Query =
                @"query($address:String!, $stakeIds:[String!]!,$skipCount:Int!,$maxResultCount:Int!){
                    getUnLockedStakeIdsAsync(input: {address:$address,stakeIds:$stakeIds,skipCount:$skipCount,maxResultCount:$maxResultCount})
            }",
            Variables = new
            {
                address = address, stakeIds = stakeIds, skipCount = 0, maxResultCount = 5000,
            }
        });

        return indexerResult.GetUnLockedStakeIdsAsync;
    }

    public async Task<List<RewardOperationRecordIndex>> GetRewardOperationRecordListAsync(string address,
        List<ExecuteStatus> executeStatus)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<RewardOperationRecordIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.Address).Value(address)));
        mustQuery.Add(q => q.Terms(i => i.Field(f => f.ExecuteStatus).Terms(executeStatus)));

        QueryContainer Filter(QueryContainerDescriptor<RewardOperationRecordIndex> f) => f.Bool(b => b.Must(mustQuery));
        var (total, list) = await _repository.GetListAsync(Filter,
            skip: 0, limit: 5000);
        return list;
    }

    public async Task<List<RewardOperationRecordIndex>> GetExecutingListAsync(string address, ExecuteType executeType)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<RewardOperationRecordIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.Address).Value(address)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.ExecuteType).Value(executeType)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.ExecuteStatus).Value(ExecuteStatus.Executing)));

        QueryContainer Filter(QueryContainerDescriptor<RewardOperationRecordIndex> f) => f.Bool(b => b.Must(mustQuery));
        var (total, list) = await _repository.GetListAsync(Filter,
            skip: 0, limit: 5000);
        return list;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService),
        ReturnDefault = ReturnDefault.Default,
        MethodName = nameof(ExceptionHandlingService.HandleException), Message = "GetClaimInfoList Indexer error")]
    public virtual async Task<long> GetRewardsCountAsync(PoolTypeEnums poolType, string address,
        int skipCount, int maxResultCount, bool filterWithdraw = false, bool filterUnlocked = false,
        List<string> liquidityIds = null)
    {
        if (string.IsNullOrEmpty(address))
        {
            return 0;
        }

        var indexerResult = await _graphQlHelper.QueryAsync<RewardsCountQuery>(new GraphQLRequest
        {
            Query =
                @"query($poolType:PoolType!, $liquidityIds:[String!]!, $filterUnlock:Boolean!,$filterWithdraw:Boolean!,$address:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getClaimInfoCount(input: {poolType:$poolType,liquidityIds:$liquidityIds,filterUnlock:$filterUnlock,filterWithdraw:$filterWithdraw,address:$address,skipCount:$skipCount,maxResultCount:$maxResultCount})
            }",
            Variables = new
            {
                poolType = poolType, filterUnlock = filterUnlocked, filterWithdraw = filterWithdraw,
                address = address, skipCount = skipCount, maxResultCount = maxResultCount,
                liquidityIds = liquidityIds ?? new List<string>()
            }
        });

        return indexerResult.GetClaimInfoCount;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), ReturnDefault = ReturnDefault.New,
        MethodName = nameof(ExceptionHandlingService.HandleException), Message = "GetMergedRewardsList Indexer error")]
    public virtual async Task<MergedRewardsListIndexerResult> GetMergedRewardsListAsync(string address,
        List<string> poolIds,
        PoolTypeEnums poolType, List<string> dappIds = null, int skipCount = 0, int maxResultCount = 5000)
    {
        if (string.IsNullOrEmpty(address))
        {
            return new MergedRewardsListIndexerResult();
        }

        var indexerResult = await _graphQlHelper.QueryAsync<MergedRewardsListQuery>(new GraphQLRequest
        {
            Query =
                @"query($poolIds:[String!]!,$address:String!,$poolType:PoolType!,$dappIds:[String!]!,$skipCount:Int!,$maxResultCount:Int!){
                    getMergedRewardsList(input: {poolIds:$poolIds,address:$address,poolType:$poolType,dappIds:$dappIds,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount
                        data{
                          account
                          amount
                          poolType
                          poolId
                          releaseTime
                          createTime
                          mergeClaimInfos{
                            claimId
                            claimedAmount
                          }
                    }
                }
            }",
            Variables = new
            {
                poolIds = poolIds ?? new List<string>(), address = address, dappIds = dappIds ?? new List<string>(),
                poolType = poolType, skipCount = skipCount, maxResultCount = maxResultCount
            }
        });

        return indexerResult.GetMergedRewardsList;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), ReturnDefault = ReturnDefault.New,
        MethodName = nameof(ExceptionHandlingService.HandleException), Message = "GetClaimInfoList Indexer error")]
    public async Task<RewardsInfoListIndexerDto> GetRewardsInfoListAsync(PoolTypeEnums poolType, string address,
        string id, int skipCount, int maxResultCount)
    {
        if (string.IsNullOrEmpty(address))
        {
            return new RewardsInfoListIndexerDto();
        }

        var indexerResult = await _graphQlHelper.QueryAsync<RewardsInfoListQuery>(new GraphQLRequest
        {
            Query =
                @"query($poolType:PoolType!,$address:String!,$id:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getRewardsInfoList(input: {poolType:$poolType,address:$address,id:$id,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        account
                        claimId
                        stakeId
                        seed
                        poolId
                        dappId
                        claimedAmount
                        claimedSymbol
                        claimedTime
                        poolType
                    }
                }
            }",
            Variables = new
            {
                poolType = poolType, address = address, id = id, skipCount = skipCount,
                maxResultCount = maxResultCount
            }
        });

        return indexerResult.GetRewardsInfoList;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), ReturnDefault = ReturnDefault.New,
        MethodName = nameof(ExceptionHandlingService.HandleException), Message = "GetConfirmBlockHeight Indexer error")]
    public async Task<long> GetConfirmBlockHeightAsync(string chainId)
    {
        var syncStateDto = await _httpProvider.InvokeAsync<SyncStateDto>(HttpMethod.Get, _indexerSyncStateOptions.Url);
        return syncStateDto.CurrentVersion.Items.First(x => x.ChainId == _indexerSyncStateOptions.ChainId)
            .LastIrreversibleBlockHeight;
    }
}