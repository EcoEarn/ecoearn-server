using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Rewards.Provider;

public interface IRewardsProvider
{
    Task<List<RewardsListIndexerDto>> GetRewardsListAsync(PoolTypeEnums poolType, string address, int skipCount,
        int maxResultCount, bool filterWithdraw = false, bool filterUnlocked = false);

    Task<List<string>> GetUnLockedStakeIdsAsync(List<string> stakeIds, string address);
}

public class RewardsProvider : IRewardsProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<RewardsProvider> _logger;

    public RewardsProvider(IGraphQlHelper graphQlHelper, ILogger<RewardsProvider> logger)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
    }

    public async Task<List<RewardsListIndexerDto>> GetRewardsListAsync(PoolTypeEnums poolType, string address,
        int skipCount, int maxResultCount, bool filterWithdraw = false, bool filterUnlocked = false)
    {
        if (string.IsNullOrEmpty(address))
        {
            return new List<RewardsListIndexerDto>();
        }

        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<RewardsListQuery>(new GraphQLRequest
            {
                Query =
                    @"query($poolType:PoolType!, $filterUnlock:Boolean!,$filterWithdraw:Boolean!,$address:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getClaimInfoList(input: {poolType:$poolType,filterUnlock:$filterUnlock,filterWithdraw:$filterWithdraw,address:$address,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        claimId,
                        poolId,
                        claimedAmount,
                        claimedSymbol,
                        claimedBlockNumber,
    					claimedTime,
    					unlockTime,
    					withdrawTime,
    					earlyStakeTime,
    					account,
    					poolType
                    }
                }
            }",
                Variables = new
                {
                    poolType = poolType, filterUnlock = filterUnlocked, filterWithdraw = filterWithdraw,
                    address = address, skipCount = skipCount, maxResultCount = maxResultCount,
                }
            });

            return indexerResult.GetClaimInfoList.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getClaimInfoList Indexer error");
            return new List<RewardsListIndexerDto>();
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
                    getUnLockedStakeIdsAsync(input: {address:$address,stakeIds:$stakeIds,skipCount:$skipCount,maxResultCount:$maxResultCount}){}
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
}