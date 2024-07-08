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
    Task<List<TokenPoolsIndexerDto>> GetTokenPoolByTokenAsync(string tokenName);
    Task<List<TokenPoolStakedInfoDto>> GetTokenPoolStakedInfoListAsync(List<string> poolIds);

    Task<List<TokenStakedIndexerDto>> GetStakedInfoListAsync(string tokenName, string address,
        List<string> pools, int skipCount = 0, int maxResultCount = 5000);
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
                            unlockWindowDuration
                            stakingToken
                            fixedBoostFactor
                            minimumAmount
                            releasePeriod
                            maximumStakeDuration
                            rewardTokenContract
                            stakeTokenContract
                            minimumClaimAmount
                            releasePeriods
                            mergeInterval
                            minimumAddLiquidityAmount
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
        var list = await GetStakedInfoListAsync(tokenName, address, new List<string>());
        return list.Count > 0 ? list[0] : new TokenStakedIndexerDto();
    }

    public async Task<Dictionary<string, TokenStakedIndexerDto>> GetAddressStakedInPoolDicAsync(List<string> poolIds,
        string address)
    {
        if (string.IsNullOrEmpty(address) || poolIds.IsNullOrEmpty())
        {
            return new Dictionary<string, TokenStakedIndexerDto>();
        }

        var list = await GetStakedInfoListAsync("", address, poolIds);
        return list.GroupBy(x => x.PoolId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    public async Task<List<TokenPoolsIndexerDto>> GetTokenPoolByTokenAsync(string tokenName)
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
                        amount,
                        tokenPoolConfig{
                            rewardToken
                            startBlockNumber
                            endBlockNumber
                            rewardPerBlock
                            unlockWindowDuration
                            stakingToken
                            fixedBoostFactor
                            minimumAmount
                            releasePeriod
                            maximumStakeDuration
                            rewardTokenContract
                            stakeTokenContract
                            minimumClaimAmount
                            releasePeriods
                            mergeInterval
                            minimumAddLiquidityAmount
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

            return indexerResult.GetTokenPoolList.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTokenPoolByTokenAsync Indexer error");
            return new List<TokenPoolsIndexerDto>();
        }
    }

    public async Task<List<TokenPoolStakedInfoDto>> GetTokenPoolStakedInfoListAsync(List<string> poolIds)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<TokenPoolStakedInfoDtoListQuery>(new GraphQLRequest
            {
                Query =
                    @"query($poolIds:[String!]!){
                    getTokenPoolStakeInfoList(input: {poolIds:$poolIds}){
                        totalCount,
                        data{
                            accTokenPerShare
                            totalStakedAmount
                            poolId
                            lastRewardTime
                    }
                }
            }",
                Variables = new
                {
                    poolIds = poolIds
                }
            });

            return indexerResult.GetTokenPoolStakeInfoList.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTokenPoolStakedInfoListAsync Indexer error");
            return new List<TokenPoolStakedInfoDto>();
        }
    }

    public async Task<List<TokenStakedIndexerDto>> GetStakedInfoListAsync(string tokenName, string address,
        List<string> pools, int skipCount = 0, int maxResultCount = 5000)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<TokenStakedListQuery>(new GraphQLRequest
            {
                Query =
                    @"query($tokenName:String!, $address:String!, $poolIds:[String!]!, $lockState:LockState!, $skipCount:Int!,$maxResultCount:Int!){
                    getStakedInfoList(input: {tokenName:$tokenName,address:$address,poolIds:$poolIds,lockState:$lockState,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        stakeId,
                        poolId,
                        account,
                        stakingToken,
                        unlockTime,
                        lastOperationTime,
                        stakingPeriod,
                        createTime,
    					updateTime,
    					poolType,
    					lockState,
                        subStakeInfos{
                            subStakeId,
                            stakedAmount,
                            stakedBlockNumber,
                            stakedTime,
                            period,
                            boostedAmount,
                            rewardDebt,
                            rewardAmount,
                            earlyStakedAmount,
                        }
                    }
                }
            }",
                Variables = new
                {
                    tokenName = string.IsNullOrEmpty(tokenName) ? "" : tokenName,
                    address = string.IsNullOrEmpty(address) ? "" : address,
                    poolIds = pools ?? new List<string>(),
                    lockState = LockState.Locking,
                    skipCount = skipCount, maxResultCount = maxResultCount,
                }
            });

            return indexerResult.GetStakedInfoList.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getStakedInfoList Indexer error");
            return new List<TokenStakedIndexerDto>();
        }
    }
}