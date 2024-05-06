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
                    @"query($keyword:String!, $dappId:String!, $sortingKeyWord:SortingKeywordType!, $sorting:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getRankingList(input: {keyword:$keyword,dappId:$dappId,sortingKeyWord:$sortingKeyWord,sorting:$sorting,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        domain,
                        address,
                        firstSymbolAmount,
                        secondSymbolAmount,
                        thirdSymbolAmount,
    					fourSymbolAmount,
    					fiveSymbolAmount,
    					sixSymbolAmount,
    					sevenSymbolAmount,
    					eightSymbolAmount,
    					nineSymbolAmount,
    					updateTime,
    					dappName,
    					role,
                    }
                }
            }",
                Variables = new
                {
                    skipCount = 0, maxResultCount = 5000,
                }
            });

            return indexerResult.GetRewardsList.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetPointsPools Indexer error");
            return new List<RewardsListIndexerDto>();
        }
    }
}