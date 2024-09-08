using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using AElf.Types;
using EcoEarn.Contracts.Rewards;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Background.Provider;
using EcoEarnServer.Background.Services.Dtos;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.Grains.Grain.StakingPoints;
using EcoEarnServer.Options;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.StakingSettlePoints;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Background.Services;

public interface IStakingPointsService
{
    Task ExecuteAsync();
}

public class StakingPointsService : IStakingPointsService, ITransientDependency
{
    private const string DailyPeriod = "86400";
    private const string LockKeyPrefix = "EcoEarnServer:StakingPoints:Lock:";

    private readonly ITokenStakingProvider _tokenStakingProvider;
    private readonly IPriceProvider _priceProvider;
    private readonly LpPoolRateOptions _lpPoolRateOptions;
    private readonly IStakingPointsProvider _stakingPointsProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<StakingPointsService> _logger;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILarkAlertProvider _larkAlertProvider;
    private readonly PointsSnapshotOptions _pointsSnapshotOptions;
    private readonly IContractProvider _contractProvider;
    private readonly IStateProvider _stateProvider;
    private readonly IAbpDistributedLock _distributedLock;


    public StakingPointsService(ITokenStakingProvider tokenStakingProvider, IPriceProvider priceProvider,
        IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions, IStakingPointsProvider stakingPointsProvider,
        IClusterClient clusterClient, IObjectMapper objectMapper, ILogger<StakingPointsService> logger,
        IDistributedEventBus distributedEventBus, ILarkAlertProvider larkAlertProvider,
        IOptionsSnapshot<PointsSnapshotOptions> pointsSnapshotOptions, IContractProvider contractProvider,
        IStateProvider stateProvider, IAbpDistributedLock distributedLock)
    {
        _tokenStakingProvider = tokenStakingProvider;
        _priceProvider = priceProvider;
        _stakingPointsProvider = stakingPointsProvider;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _logger = logger;
        _distributedEventBus = distributedEventBus;
        _larkAlertProvider = larkAlertProvider;
        _contractProvider = contractProvider;
        _stateProvider = stateProvider;
        _distributedLock = distributedLock;
        _pointsSnapshotOptions = pointsSnapshotOptions.Value;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
    }

    public async Task ExecuteAsync()
    {
        await using var handle = await _distributedLock.TryAcquireAsync(name: LockKeyPrefix);

        if (handle == null)
        {
            _logger.LogWarning("do not get lock, keys already exits.");
            return;
        }

        if (await _stateProvider.CheckStateAsync(StateGeneratorHelper.StakingPointsKey()))
        {
            _logger.LogInformation("today has already created points snapshot.");
            return;
        }

        try
        {
            var addressStakingSettlePointsList = await GetAddressStakingSettlePointsListAsync();

            var failInfos = new List<string>();
            var listEto = new List<AddressStakingSettlePointsEto>();
            var settleListEto = new List<AddressStakingSettlePointsEto>();
            foreach (var addressStakingSettlePointsDto in addressStakingSettlePointsList)
            {
                var id = addressStakingSettlePointsDto.Id;
                var addressStakingSettlePointsGrain = _clusterClient.GetGrain<IAddressStakingSettlePointsGrain>(id);
                var result = await addressStakingSettlePointsGrain.CreateOrUpdateAsync(addressStakingSettlePointsDto);

                if (!result.Success)
                {
                    _logger.LogError("address settle staking points fail, message:{message}, id: {id}", result.Message,
                        id);
                    failInfos.Add(JsonConvert.SerializeObject(addressStakingSettlePointsDto));
                }
                settleListEto.Add(
                    _objectMapper.Map<AddressStakingSettlePointsDto, AddressStakingSettlePointsEto>(addressStakingSettlePointsDto));
                listEto.Add(
                    _objectMapper.Map<AddressStakingSettlePointsDto, AddressStakingSettlePointsEto>(result.Data));
            }

            await _distributedEventBus.PublishAsync(new AddressStakingSettlePointsListEto
            {
                EventDataList = listEto
            });

            var transactionFailMessages = await BatchSettleAsync(settleListEto);
            failInfos.AddRange(transactionFailMessages);

            if (failInfos.Count > 0)
            {
                await _larkAlertProvider.SendLarkFailAlertAsync(JsonConvert.ToString(failInfos));
            }

            await _stateProvider.SetStateAsync(StateGeneratorHelper.StakingPointsKey(), true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "StakingPoints fail.");
            await _stateProvider.SetStateAsync(StateGeneratorHelper.StakingPointsKey(), false);
            await _larkAlertProvider.SendLarkFailAlertAsync(e.Message);
        }
    }

    private async Task<List<string>> BatchSettleAsync(List<AddressStakingSettlePointsEto> settlePointsList)
    {
        var failMessages = new List<string>();
        var chainId = _pointsSnapshotOptions.ChainId;
        var actionName = _pointsSnapshotOptions.StakingPointsActionName;
        var recurCount = settlePointsList.Count / _pointsSnapshotOptions.BatchStakingPointsSettleCount + 1;
        for (var i = 0; i < recurCount; i++)
        {
            var skipCount = _pointsSnapshotOptions.BatchStakingPointsSettleCount * i;
            var list = settlePointsList.Skip(skipCount).Take(_pointsSnapshotOptions.BatchStakingPointsSettleCount).ToList();

            if (list.IsNullOrEmpty()) return failMessages;

            var userPoints = list
                .Where(item => decimal.Parse(item.Points) > 0)
                .Select(item => new UserPoints
                {
                    UserAddress = Address.FromBase58(item.Address),
                    UserPointsValue = ConvertBigInteger(decimal.Parse(item.Points), 8)
                }).ToList();
            var batchSettleInput = new BatchSettleInput
            {
                ActionName = actionName,
                UserPointsList = { userPoints }
            };
            var transaction = _contractProvider
                .CreateTransaction(chainId, ContractConstants.BatchSettleSenderName, ContractConstants.RewardsContractName,
                    ContractConstants.BatchSettleMethodName, batchSettleInput)
                .Result
                .transaction;
            var transactionOutput = await _contractProvider.SendTransactionAsync(chainId, transaction);

            var transactionResult =
                await _contractProvider.CheckTransactionStatusAsync(transactionOutput.TransactionId, chainId);
            if (!transactionResult.Result)
            {
                failMessages.Add(transactionResult.Error);
            }

            await Task.Delay(_pointsSnapshotOptions.TaskDelayMilliseconds);
        }

        return failMessages;
    }

    private async Task<List<AddressStakingSettlePointsDto>> GetAddressStakingSettlePointsListAsync()
    {
        var tokenStakedIndexerDtos = await GetAllStakeInfoListAsync();

        var allPools = await _tokenStakingProvider.GetTokenPoolsAsync(new GetTokenPoolsInput
        {
            PoolType = PoolTypeEnums.All
        });
        var poolIdDic = allPools.GroupBy(x => x.PoolId).ToDictionary(g => g.Key, g => g.First());
        var stakingPointsDtos = new List<StakingPointsDto>();

        foreach (var tokenStakedIndexerDto in tokenStakedIndexerDtos)
        {
            if (!poolIdDic.TryGetValue(tokenStakedIndexerDto.PoolId, out var poolInfo))
            {
                continue;
            }

            var stakingToken = poolInfo.TokenPoolConfig.StakingToken;
            var currencyPair = $"{stakingToken.ToUpper()}_USDT";

            double rate;
            if (poolInfo.PoolType == PoolTypeEnums.Token)
            {
                rate = await _priceProvider.GetGateIoPriceAsync(currencyPair);
            }
            else
            {
                var feeRate = _lpPoolRateOptions.LpPoolRateDic.TryGetValue(
                    poolInfo.TokenPoolConfig.StakeTokenContract,
                    out var poolRate)
                    ? poolRate
                    : 0;
                rate = await _priceProvider.GetLpPriceAsync(stakingToken, feeRate);
            }

            var points = decimal.Zero;
            foreach (var subStakeInfoIndexerDto in tokenStakedIndexerDto.SubStakeInfos)
            {
                var stakeAmount = subStakeInfoIndexerDto.StakedAmount + subStakeInfoIndexerDto.EarlyStakedAmount;
                var k = 1 + decimal.Parse(subStakeInfoIndexerDto.Period.ToString()) / decimal.Parse(DailyPeriod) /
                    decimal.Parse(poolInfo.TokenPoolConfig.FixedBoostFactor.ToString());
                points += decimal.Parse(stakeAmount.ToString()) / 100000000 *
                          Convert.ToDecimal(rate.ToString("F10")) * k;
            }

            stakingPointsDtos.Add(new StakingPointsDto
            {
                Address = tokenStakedIndexerDto.Account,
                Points = points,
                DappId = poolInfo.DappId
            });
        }

        var addressPointsList = stakingPointsDtos
            .GroupBy(x => x.Address)
            .Select(g => new AddressStakingPointsDto
            {
                Address = g.Key,
                Points = g.Sum(x => x.Points),
                DappPoints = g.GroupBy(x => x.DappId).Select(dappGroup => new StakingPointsDto
                {
                    DappId = dappGroup.Key,
                    Points = dappGroup.Sum(x => x.Points)
                }).ToList()
            })
            .ToList();

        return _objectMapper.Map<List<AddressStakingPointsDto>, List<AddressStakingSettlePointsDto>>(addressPointsList);
    }


    private async Task<List<TokenStakedIndexerDto>> GetAllStakeInfoListAsync()
    {
        var res = new List<TokenStakedIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<TokenStakedIndexerDto> list;
        do
        {
            list = await _tokenStakingProvider.GetStakedInfoListAsync("", "", new List<string>(), skipCount,
                maxResultCount);
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < maxResultCount)
            {
                break;
            }

            skipCount += count;
        } while (!list.IsNullOrEmpty());

        return res;
    }

    private static BigIntValue ConvertBigInteger(decimal num, int places)
    {
        var multiplier = (decimal)Math.Pow(10, places);
        var bigInt = new BigInteger(Math.Floor(num * multiplier));
        var result = new BigIntValue { Value = bigInt.ToString() };
        return result;
    }
}