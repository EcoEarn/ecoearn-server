using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.Rewards.Dtos;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Rewards.Provider;

public interface IRewardsProvider
{
    Task<List<RewardsListIndexerDto>> GetRewardsListAsync(GetRewardsListInput input);
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

    public async Task<List<RewardsListIndexerDto>> GetRewardsListAsync(GetRewardsListInput input)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<RewardsListQuery>(new GraphQLRequest
            {
                Query =
                    @"query($poolType:PoolType, $filterUnlocked:Boolean, $skipCount:Int!,$maxResultCount:Int!){
                    getClaimInfoList(input: {poolType:$poolType,filterUnlocked:$filterUnlocked,skipCount:$skipCount,maxResultCount:$maxResultCount}){
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
                    poolType = input.PoolType, filterUnlocked = input.FilterUnlocked, oskipCount = 0,
                    maxResultCount = 5000,
                }
            });

            return indexerResult.GetRewardsList.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getClaimInfoList Indexer error");
            return new List<RewardsListIndexerDto>();
        }
    }
}