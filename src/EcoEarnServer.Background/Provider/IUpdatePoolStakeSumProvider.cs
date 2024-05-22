using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Types;
using EcoEarn.Contracts.Tokens;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.TokenStaking.Provider;
using GraphQL;
using Hangfire;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface IUpdatePoolStakeSumProvider
{
    Task<List<StakedInfoIndexerDto>> GetStakedInfoListAsync(int skipCount, int maxResultCount);
    Task<long> GetConfirmedBlockHeight();

    Task ExecuteUpdateStakeAsync(string stakeId);
}

public class UpdatePoolStakeSumProvider : IUpdatePoolStakeSumProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<UpdatePoolStakeSumProvider> _logger;
    private readonly IContractProvider _contractProvider;

    public UpdatePoolStakeSumProvider(IGraphQlHelper graphQlHelper, ILogger<UpdatePoolStakeSumProvider> logger,
        IContractProvider contractProvider)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
        _contractProvider = contractProvider;
    }

    public async Task<List<StakedInfoIndexerDto>> GetStakedInfoListAsync(int skipCount, int maxResultCount)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<StakedListQuery>(new GraphQLRequest
            {
                Query =
                    @"query($tokenName:String!, $address:String!, $poolIds:[String!]!, $lockState:LockState!, $skipCount:Int!,$maxResultCount:Int!){
                    getStakedInfoList(input: {tokenName:$tokenName,address:$address,poolIds:$poolIds,lockState:$lockState,skipCount:$skipCount,maxResultCount:$maxResultCount}){
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
    					stakingPeriod,
    					createTime,
    					updateTime,
    					poolType,
                    }
                }
            }",
                Variables = new
                {
                    tokenName = "", address = "", poolIds = new List<string>(),
                    lockState = LockState.Locking, skipCount = skipCount, maxResultCount = maxResultCount,
                }
            });

            var list = indexerResult.GetStakedInfoList;
            return list.Data.Count > 0 ? list.Data : new List<StakedInfoIndexerDto>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Worker GetStakedInfoList Indexer error");
            return new List<StakedInfoIndexerDto>();
        }
    }

    public async Task<long> GetConfirmedBlockHeight()
    {
        var indexerResult = await _graphQlHelper.QueryAsync<ConfirmedBlockHeightRecord>(new GraphQLRequest
        {
            Query = @"
			    query($chainId:String!,$filterType:BlockFilterType!) {
                    getConfirmedBlockHeight(input: {chainId:$chainId,filterType:$filterType}){
                        confirmedBlockHeight
                }
            }",
            Variables = new
            {
                chainId = "tDVW", filterType = BlockFilterType.LOG_EVENT
            }
        });

        return indexerResult.GetConfirmedBlockHeight.ConfirmedBlockHeight;
    }

    public async Task ExecuteUpdateStakeAsync(string stakeId)
    {
        var updateStakeInfoInput = new UpdateStakeInfoInput
        {
            StakeIds = { Hash.LoadFromHex(stakeId) }
        };

        var transaction = _contractProvider
            .CreateTransaction("tDVW", ContractConstants.SenderName, ContractConstants.ContractName,
                ContractConstants.UpdateStakeInfoSenderName, updateStakeInfoInput)
            .Result
            .transaction;
        var transactionResult = await _contractProvider.SendTransactionAsync("tDVW", transaction);
        //check transaction result
        BackgroundJob.Schedule(
            () => CheckTransactionResultAsync(transactionResult.TransactionId, stakeId),
            TimeSpan.FromMilliseconds(3000));
    }

    public async Task CheckTransactionResultAsync(string transactionId, string stakeId)
    {
        var txResult = await _contractProvider.QueryTransactionResult(transactionId, "tDVW");
        if (txResult.Status == TransactionState.Pending)
        {
            _logger.LogWarning("transaction pending.");
            BackgroundJob.Schedule(() => CheckTransactionResultAsync(transactionId, stakeId),
                TimeSpan.FromMilliseconds(1000));
            return;
        }

        if (!string.IsNullOrEmpty(txResult.Error) && txResult.Error.Contains("Already updated."))
        {
            _logger.LogWarning("Already updated.");
            return;
        }

        // retry
        if (txResult.Status != TransactionState.Mined)
        {
            _logger.LogWarning("stakeId update stake sum transaction not Mined, {stakeId}", stakeId);
            await ExecuteUpdateStakeAsync(stakeId);
            //TODO Alert
            return;
        }

        _logger.LogInformation("stakeId update stake sum transaction successful, {stakeId}", stakeId);
    }
}