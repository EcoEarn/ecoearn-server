using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Common.GraphQL;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface IStakingPointsProvider
{
    Task<List<ReferralRecordDto>> GetReferralRecordAsync(int skipCount, int maxResultCount);
}

public class StakingPointsProvider : IStakingPointsProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<StakingPointsProvider> _logger;

    public StakingPointsProvider(ILogger<StakingPointsProvider> logger, IGraphQlHelper graphQlHelper)
    {
        _logger = logger;
        _graphQlHelper = graphQlHelper;
    }

    public async Task<List<ReferralRecordDto>> GetReferralRecordAsync(int skipCount, int maxResultCount)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<ReferralRecordQuery>(new GraphQLRequest
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
                    skipCount = skipCount, maxResultCount = maxResultCount
                }
            });

            return indexerResult.GetReferralRecordList.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetReferralRecordList Indexer error");
            return new List<ReferralRecordDto>();
        }
    }
}