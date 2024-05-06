using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.Common.GraphQL;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.TokenStaking.Provider;

public interface ITokenStakingProvider
{
    Task<List<TokenPoolsIndexerDto>> GetTokenPoolsAsync();
}

public class TokenStakingProvider : ITokenStakingProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<TokenStakingProvider> _logger;

    public TokenStakingProvider(IGraphQlHelper graphQlHelper, ILogger<TokenStakingProvider> logger)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
    }

    public async Task<List<TokenPoolsIndexerDto>> GetTokenPoolsAsync()
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<TokenPoolsQuery>(new GraphQLRequest
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

            return indexerResult.GetTokenPools.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetPointsPools Indexer error");
            return new List<TokenPoolsIndexerDto>();
        }
    }
}