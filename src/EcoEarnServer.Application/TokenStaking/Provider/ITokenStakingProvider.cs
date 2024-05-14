using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.TokenStaking.Dtos;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.TokenStaking.Provider;

public interface ITokenStakingProvider
{
    Task<List<TokenPoolsIndexerDto>> GetTokenPoolsAsync(GetTokenPoolsInput input);
    Task<TokenStakedIndexerDto> GetStakedInfoAsync(string tokenName, string address);

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

    public async Task<List<TokenPoolsIndexerDto>> GetTokenPoolsAsync(GetTokenPoolsInput input)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<TokenPoolsQuery>(new GraphQLRequest
            {
                Query =
                    @"query($tokenName:String!, $poolType:PoolType!, $poolIds:[String!]!,$skipCount:Int!,$maxResultCount:Int!){
                    getTokenPoolList(input: {tokenName:$tokenName,poolType:$poolType,poolIds:$poolIds,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        dappId,
                        poolId,
                        amount,
                        tokenPoolConfig{
                            rewardToken
                            startBlockNumber
                            endBlockNumber
                            rewardPerBlock
                            updateAddress
                            stakingToken
                            fixedBoostFactor
                            minimumAmount
                            releasePeriod
                            maximumStakeDuration
                            rewardTokenContract
                            stakeTokenContract
                            minimumClaimAmount
                        },
    					createTime,
    					poolType
                    }
                }
            }",
                Variables = new
                {
                    tokenName = "", poolType = input.PoolType, skipCount = 0, maxResultCount = 5000,
                    poolIds = input.PoolIds ?? new List<string>()
                }
            });

            return indexerResult.GetTokenPoolList.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetPointsPools Indexer error");
            return new List<TokenPoolsIndexerDto>();
        }
    }

    public async Task<TokenStakedIndexerDto> GetStakedInfoAsync(string tokenName, string address)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<TokenStakedQuery>(new GraphQLRequest
            {
                Query =
                    @"query($tokenName:String!, $address:String!, $poolIds:[String!]!, $skipCount:Int!,$maxResultCount:Int!){
                    getStakedInfoList(input: {tokenName:$tokenName,address:$address,poolIds:$poolIds,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        stakeId,
                        poolId,
                        stakingToken,
                        stakedAmount,
                        earlyStakedAmount,
    					claimedAmount,
    					stakedBlockNumber,
    					stakedTime,
    					period,
    					account,
    					boostedAmount,
    					rewardDebt,
    					withdrawTime,
    					rewardAmount,
    					lockedRewardAmount,
    					lastOperationTime,
    					createTime,
    					updateTime,
    					poolType,
                    }
                }
            }",
                Variables = new
                {
                    tokenName = tokenName, address = address, poolIds = new List<string>(), skipCount = 0,
                    maxResultCount = 5000,
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
        if (string.IsNullOrEmpty(address) || poolIds.IsNullOrEmpty())
        {
            return new Dictionary<string, TokenStakedIndexerDto>();
        }

        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<TokenStakedListQuery>(new GraphQLRequest
            {
                Query =
                    @"query($address:String!,$tokenName:String!,$poolIds:[String!]!, $skipCount:Int!,$maxResultCount:Int!){
                    getStakedInfoList(input: {address:$address,tokenName:$tokenName,poolIds:$poolIds,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        stakeId,
                        poolId,
                        stakingToken,
                        stakedAmount,
                        earlyStakedAmount,
    					claimedAmount,
    					stakedBlockNumber,
    					stakedTime,
    					period,
    					account,
    					boostedAmount,
    					rewardDebt,
    					withdrawTime,
    					rewardAmount,
    					lockedRewardAmount,
    					lastOperationTime,
    					createTime,
    					updateTime,
    					poolType,
                    }
                }
            }",
                Variables = new
                {
                    address = address, tokenName = "", poolIds = poolIds, skipCount = 0, maxResultCount = 5000,
                }
            });
            return indexerResult.GetStakedInfoList.Data.GroupBy(x => x.PoolId)
                .ToDictionary(g => g.Key, g => g.First());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getStakedInfoList Indexer error");
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
                    @"query($tokenName:String!, $poolType:PoolType!, $poolIds:[String!]!, $skipCount:Int!,$maxResultCount:Int!){
                    getTokenPoolList(input: {tokenName:$tokenName,poolType:$poolType,poolIds:$poolIds,skipCount:$skipCount,maxResultCount:$maxResultCount}){
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
                            minimumAmount
                            releasePeriod
                            maximumStakeDuration
                            rewardTokenContract
                            stakeTokenContract
                            minimumClaimAmount
                        },
    					createTime,
    					poolType
                    }
                }
            }",
                Variables = new
                {
                    tokenName = tokenName, poolType = PoolTypeEnums.All, poolIds = new List<string>(), skipCount = 0,
                    maxResultCount = 5000,
                }
            });

            return indexerResult.GetTokenPoolList.Data[0];
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTokenPoolByTokenAsync Indexer error");
            return new TokenPoolsIndexerDto();
        }
    }
}