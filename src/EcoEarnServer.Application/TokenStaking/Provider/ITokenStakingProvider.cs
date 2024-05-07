using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Common.GraphQL;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.TokenStaking.Provider;

public interface ITokenStakingProvider
{
    Task<List<TokenPoolsIndexerDto>> GetTokenPoolsAsync();
    Task<TokenStakedIndexerDto> GetStakedInfoAsync(string tokenName);

    Task<Dictionary<string, TokenStakedIndexerDto>> GetAddressStakedInPoolDicAsync(List<string> pools, string address);
    Task<TokenPoolsIndexerDto> GetTokenPoolByTokenAsync(string tokenName);
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
                    getTokenPoolList(input: {keyword:$keyword,dappId:$dappId,sortingKeyWord:$sortingKeyWord,sorting:$sorting,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        dappId,
                        poolId,
                        poolAddress,
                        amount,
                        tokenPoolConfig{
                            rewardToken
                            startBlockNumber
                            endBlockNumber
                            rewardPerBlock
                            updateAddress
                            stakingToken
                            fixedBoostFactor
                            minimalAmount
                            releasePeriod
                            maximumStakeDuration
                            rewardTokenContract
                            stakeTokenContract
                            minimalClaimAmount
                        },
    					createTime
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

    public async Task<TokenStakedIndexerDto> GetStakedInfoAsync(string tokenName)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<TokenStakedQuery>(new GraphQLRequest
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

            return indexerResult.GetTokenStakedInfo;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetPointsPools Indexer error");
            return new TokenStakedIndexerDto();
        }
    }

    public async Task<Dictionary<string, TokenStakedIndexerDto>> GetAddressStakedInPoolDicAsync(List<string> poolIds,
        string address)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<TokenStakedListQuery>(new GraphQLRequest
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
            return indexerResult.GetTokenStakedInfoList.Data.GroupBy(x => x.PoolId)
                .ToDictionary(g => g.Key, g => g.First());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetPointsPools Indexer error");
            return new Dictionary<string, TokenStakedIndexerDto>();
        }
    }

    public async Task<TokenPoolsIndexerDto> GetTokenPoolByTokenAsync(string tokenName)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<TokenPoolsQuery>(new GraphQLRequest
            {
                Query =
                    @"query($keyword:String!, $dappId:String!, $sortingKeyWord:SortingKeywordType!, $sorting:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getTokenPoolList(input: {keyword:$keyword,dappId:$dappId,sortingKeyWord:$sortingKeyWord,sorting:$sorting,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        dappId,
                        poolId,
                        poolAddress,
                        amount,
                        tokenPoolConfig{
                            rewardToken
                            startBlockNumber
                            endBlockNumber
                            rewardPerBlock
                            updateAddress
                            stakingToken
                            fixedBoostFactor
                            minimalAmount
                            releasePeriod
                            maximumStakeDuration
                            rewardTokenContract
                            stakeTokenContract
                            minimalClaimAmount
                        },
    					createTime
                    }
                }
            }",
                Variables = new
                {
                    skipCount = 0, maxResultCount = 1,
                }
            });

            return indexerResult.GetTokenPools.Data[0];
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetPointsPools Indexer error");
            return new TokenPoolsIndexerDto();
        }
    }
}