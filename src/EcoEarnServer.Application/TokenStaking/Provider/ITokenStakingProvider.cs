using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.ExceptionHandle;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.StakingSettlePoints;
using EcoEarnServer.TokenStaking.Dtos;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.TokenStaking.Provider;

public interface ITokenStakingProvider
{
    Task<List<TokenPoolsIndexerDto>> GetTokenPoolsAsync(GetTokenPoolsInput input);
    Task<List<TokenStakedIndexerDto>> GetStakedInfoAsync(string tokenName, string address, List<string> poolIds);

    Task<Dictionary<string, TokenStakedIndexerDto>> GetAddressStakedInPoolDicAsync(List<string> pools, string address);
    Task<List<TokenPoolsIndexerDto>> GetTokenPoolByTokenAsync(string tokenName, PoolTypeEnums poolType);
    Task<List<TokenPoolStakedInfoDto>> GetTokenPoolStakedInfoListAsync(List<string> poolIds);

    Task<List<TokenStakedIndexerDto>> GetStakedInfoListAsync(string tokenName, string address,
        List<string> pools, int skipCount = 0, int maxResultCount = 5000);

    Task<List<StakeCountIndex>> GetStakeCountListAsync();
}

public class TokenStakingProvider : ITokenStakingProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<TokenStakingProvider> _logger;
    private readonly INESTRepository<StakeCountIndex, string> _repository;

    public TokenStakingProvider(IGraphQlHelper graphQlHelper, ILogger<TokenStakingProvider> logger,
        INESTRepository<StakeCountIndex, string> repository)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
        _repository = repository;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), ReturnDefault = ReturnDefault.New,
        MethodName = nameof(ExceptionHandlingService.HandleException), Message = "GetPointsPools Indexer error.")]
    public virtual async Task<List<TokenPoolsIndexerDto>> GetTokenPoolsAsync(GetTokenPoolsInput input)
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
                            swapContract
                            minimumClaimAmount
                            releasePeriods
                            mergeInterval
                            minimumAddLiquidityAmount
                            stakeAddress
                            rewardAddress
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

    public async Task<List<TokenStakedIndexerDto>> GetStakedInfoAsync(string tokenName, string address,
        List<string> poolIds)
    {
        var list = await GetStakedInfoListAsync(tokenName, address, poolIds);
        return list;
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), ReturnDefault = ReturnDefault.New,
        MethodName = nameof(ExceptionHandlingService.HandleException),
        Message = "GetTokenPoolByTokenAsync Indexer error")]
    public virtual async Task<List<TokenPoolsIndexerDto>> GetTokenPoolByTokenAsync(string tokenName,
        PoolTypeEnums poolType)
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
                            swapContract
                            minimumClaimAmount
                            releasePeriods
                            mergeInterval
                            minimumAddLiquidityAmount
                            stakeAddress
                            rewardAddress
                        },
    					createTime,
    					poolType
                    }
                }
            }",
            Variables = new
            {
                tokenName = tokenName, poolType = poolType, poolIds = new List<string>(), skipCount = 0,
                maxResultCount = 5000,
            }
        });

        return indexerResult.GetTokenPoolList.Data;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), ReturnDefault = ReturnDefault.New,
        MethodName = nameof(ExceptionHandlingService.HandleException),
        Message = "GetTokenPoolStakedInfoListAsync Indexer error")]
    public virtual async Task<List<TokenPoolStakedInfoDto>> GetTokenPoolStakedInfoListAsync(List<string> poolIds)
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), ReturnDefault = ReturnDefault.New,
        MethodName = nameof(ExceptionHandlingService.HandleException), Message = "GetStakedInfoList Indexer error")]
    public virtual async Task<List<TokenStakedIndexerDto>> GetStakedInfoListAsync(string tokenName, string address,
        List<string> pools, int skipCount = 0, int maxResultCount = 5000)
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

    public async Task<List<StakeCountIndex>> GetStakeCountListAsync()
    {
        var result = await _repository.GetListAsync();
        return result.Item2;
    }
}