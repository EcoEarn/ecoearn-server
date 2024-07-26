using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using GraphQL;
using Microsoft.Extensions.Logging;
using Nest;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Rewards.Provider;

public interface IRewardsProvider
{
    Task<RewardsListIndexerResult> GetRewardsListAsync(PoolTypeEnums poolType, string address, int skipCount,
        int maxResultCount, bool filterWithdraw = false, bool filterUnlocked = false, List<string> liquidityIds = null);

    Task<List<string>> GetUnLockedStakeIdsAsync(List<string> stakeIds, string address);

    Task<List<RewardOperationRecordIndex>> GetRewardOperationRecordListAsync(string address,
        List<ExecuteStatus> executeStatus);

    Task<List<RewardOperationRecordIndex>> GetExecutingListAsync(string address, ExecuteType executeType);

    Task<long> GetRewardsCountAsync(PoolTypeEnums poolType, string address, int skipCount,
        int maxResultCount, bool filterWithdraw = false, bool filterUnlocked = false, List<string> liquidityIds = null);

    Task<MergedRewardsListIndexerResult> GetMergedRewardsListAsync(string address, PoolTypeEnums poolType,
        int skipCount, int maxResultCount);
}

public class RewardsProvider : IRewardsProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<RewardsProvider> _logger;
    private readonly INESTRepository<RewardOperationRecordIndex, string> _repository;

    public RewardsProvider(IGraphQlHelper graphQlHelper, ILogger<RewardsProvider> logger,
        INESTRepository<RewardOperationRecordIndex, string> repository)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
        _repository = repository;
    }

    public async Task<RewardsListIndexerResult> GetRewardsListAsync(PoolTypeEnums poolType, string address,
        int skipCount, int maxResultCount, bool filterWithdraw = false, bool filterUnlocked = false,
        List<string> liquidityIds = null)
    {
        if (string.IsNullOrEmpty(address))
        {
            return new RewardsListIndexerResult();
        }

        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<RewardsListQuery>(new GraphQLRequest
            {
                Query =
                    @"query($poolType:PoolType!, $liquidityIds:[String!]!, $filterUnlock:Boolean!,$filterWithdraw:Boolean!,$address:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getClaimInfoList(input: {poolType:$poolType,liquidityIds:$liquidityIds,filterUnlock:$filterUnlock,filterWithdraw:$filterWithdraw,address:$address,skipCount:$skipCount,maxResultCount:$maxResultCount}){
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
                    liquidityIds = liquidityIds ?? new List<string>()
                }
            });

            return indexerResult.GetClaimInfoList;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getClaimInfoList Indexer error");
            return new RewardsListIndexerResult();
        }
    }

    public async Task<List<string>> GetUnLockedStakeIdsAsync(List<string> stakeIds, string address)
    {
        if (string.IsNullOrEmpty(address) || stakeIds.IsNullOrEmpty())
        {
            return new List<string>();
        }

        try
        {
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
        catch (Exception e)
        {
            _logger.LogError(e, "GetUnLockedStakeIds Indexer error");
            return new List<string>();
        }
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

    public async Task<long> GetRewardsCountAsync(PoolTypeEnums poolType, string address,
        int skipCount, int maxResultCount, bool filterWithdraw = false, bool filterUnlocked = false,
        List<string> liquidityIds = null)
    {
        if (string.IsNullOrEmpty(address))
        {
            return 0;
        }

        try
        {
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
        catch (Exception e)
        {
            _logger.LogError(e, "getClaimInfoList Indexer error");
            return 0;
        }
    }

    public async Task<MergedRewardsListIndexerResult> GetMergedRewardsListAsync(string address, PoolTypeEnums poolType,
        int skipCount, int maxResultCount)
    {
        if (string.IsNullOrEmpty(address))
        {
            return new MergedRewardsListIndexerResult();
        }

        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<MergedRewardsListQuery>(new GraphQLRequest
            {
                Query =
                    @"query($poolType:PoolType!,$address:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getMergedRewardsList(input: {poolType:$poolType,address:$address,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount
                        data{
                          account
                          amount
                          poolType
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
                    poolType = poolType, address = address, skipCount = skipCount, maxResultCount = maxResultCount
                }
            });

            return indexerResult.GetMergedRewardsList;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetMergedRewardsList Indexer error");
            return new MergedRewardsListIndexerResult();
        }
    }
}