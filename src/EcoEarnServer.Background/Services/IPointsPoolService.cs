using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Background.Provider;
using EcoEarnServer.Common;
using EcoEarnServer.Constants;
using EcoEarnServer.Grains.Grain.PointsPool;
using EcoEarnServer.Grains.Grain.PointsStakeRewards;
using EcoEarnServer.PointsPool;
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.PointsStakeRewards;
using Hangfire;
using Microsoft.Extensions.Logging;
using Orleans;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Background.Services;

public interface IPointsPoolService
{
    Task BatchUpdatePointsPoolAddressStakeAsync(List<PointsSnapshotIndex> pointsSnapshots,
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic, int settleRewardsBeforeDays,
        Dictionary<string, List<PointsStakeRewardsIndex>> endedPeriodRewardsDic);

    Task UpdatePointsPoolStakeSumAsync(Dictionary<string, PointsPoolStakeSumDto> stakeSumDic);
}

public class PointsPoolService : IPointsPoolService, ISingletonDependency
{
    private readonly IClusterClient _clusterClient;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILogger<PointsPoolService> _logger;
    private readonly IObjectMapper _objectMapper;
    private readonly ILarkAlertProvider _larkAlertProvider;

    public PointsPoolService(IClusterClient clusterClient, IDistributedEventBus distributedEventBus,
        ILogger<PointsPoolService> logger, IObjectMapper objectMapper, ILarkAlertProvider larkAlertProvider)
    {
        _clusterClient = clusterClient;
        _distributedEventBus = distributedEventBus;
        _logger = logger;
        _objectMapper = objectMapper;
        _larkAlertProvider = larkAlertProvider;
    }


    [AutomaticRetry(Attempts = 20, DelaysInSeconds = new[] { 40 })]
    public async Task BatchUpdatePointsPoolAddressStakeAsync(List<PointsSnapshotIndex> pointsSnapshots,
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic, int beforeDays,
        Dictionary<string, List<PointsStakeRewardsIndex>> endedPeriodRewardsDic)
    {
        var stakeListEto = new List<PointsPoolAddressStakeEto>();
        var rewardsList = new List<PointsStakeRewardsEto>();
        var rewardsSumList = new List<PointsStakeRewardsSumEto>();
        foreach (var pointsSnapshotIndex in pointsSnapshots)
        {
            stakeListEto.AddRange(await UpdatePointsPoolAddressStakeSumAsync(pointsSnapshotIndex, stakeSumDic));
            rewardsList.AddRange(await RecordPeriodRewardsAsync(pointsSnapshotIndex, stakeSumDic, beforeDays));
            rewardsSumList.AddRange(
                await UpdateAddressRewardsSumAsync(pointsSnapshotIndex, stakeSumDic, endedPeriodRewardsDic));
        }

        if (stakeListEto.Count > 0)
        {
            var pointsPoolAddressStakeListEto = new PointsPoolAddressStakeListEto { EventDataList = stakeListEto };
            await _distributedEventBus.PublishAsync(pointsPoolAddressStakeListEto);
        }

        if (rewardsList.Count > 0)
        {
            var rewardsListEto = new PointsStakeRewardsListEto { EventDataList = rewardsList };
            await _distributedEventBus.PublishAsync(rewardsListEto);
        }

        if (rewardsSumList.Count > 0)
        {
            var rewardsSumListEto = new PointsStakeRewardsSumListEto { EventDataList = rewardsSumList };
            await _distributedEventBus.PublishAsync(rewardsSumListEto);
        }
    }

    private async Task<List<PointsPoolAddressStakeEto>> UpdatePointsPoolAddressStakeSumAsync(
        PointsSnapshotIndex pointsSnapshot, Dictionary<string, PointsPoolStakeSumDto> stakeSumDic)
    {
        var stakeListEto = new List<PointsPoolAddressStakeEto>();
        foreach (var (poolIndex, pointsPoolStakeSumDto) in stakeSumDic)
        {
            var value = CheckPoints(poolIndex, pointsPoolStakeSumDto.PoolId, pointsSnapshot);

            if (value == "0")
            {
                continue;
            }

            //update the staked amount for each address in each points pool
            var id = GuidHelper.GenerateId(pointsSnapshot.Address, pointsPoolStakeSumDto.PoolId);
            var poolAddressStakeDto = new PointsPoolAddressStakeDto
            {
                Id = id,
                PoolId = pointsPoolStakeSumDto.PoolId,
                PoolName = pointsPoolStakeSumDto.PoolName,
                DappId = pointsPoolStakeSumDto.DappId,
                Address = pointsSnapshot.Address,
                StakeAmount = value
            };

            stakeListEto.Add(
                _objectMapper.Map<PointsPoolAddressStakeDto, PointsPoolAddressStakeEto>(poolAddressStakeDto));
        }

        return stakeListEto;
    }

    private async Task<List<PointsStakeRewardsEto>> RecordPeriodRewardsAsync(PointsSnapshotIndex pointsSnapshot,
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic, int settleRewardsBeforeDays)
    {
        var today = DateTime.UtcNow;
        var settleDate = today.AddDays(settleRewardsBeforeDays).ToString("yyyyMMdd");
        var rewardsList = new List<PointsStakeRewardsEto>();
        foreach (var (poolIndex, pointsPoolStakeSumDto) in stakeSumDic)
        {
            var value = CheckPoints(poolIndex, pointsPoolStakeSumDto.PoolId, pointsSnapshot);

            if (value == "0")
            {
                continue;
            }

            //record the rewards of the previous day
            var stakeAmount = decimal.Parse(pointsPoolStakeSumDto.StakeAmount);
            var totalRewards = stakeAmount == 0
                ? 0
                : Math.Floor(decimal.Parse(value) / stakeAmount * pointsPoolStakeSumDto.DailyReward *
                             100000000) / 100000000;
            var rewards = Math.Floor(totalRewards / pointsPoolStakeSumDto.ReleasePeriod * 100000000) / 100000000;
            var rewardsId = GuidHelper.GenerateId(pointsSnapshot.Address, poolIndex, settleDate);
            var rewardsEto = new PointsStakeRewardsEto
            {
                Id = rewardsId,
                Address = pointsSnapshot.Address,
                PoolId = pointsPoolStakeSumDto.PoolId,
                PoolName = pointsPoolStakeSumDto.PoolName,
                DappId = pointsPoolStakeSumDto.DappId,
                Rewards = rewards.ToString(CultureInfo.InvariantCulture),
                SettleDate = settleDate,
                CreateTime = DateTime.UtcNow.ToUtcMilliSeconds(),
                PeriodState = PeriodState.InPeriod,
                StartSettleDate = today.ToString("yyyyMMdd"),
                EndSettleDate = today.AddDays(pointsPoolStakeSumDto.ReleasePeriod).ToString("yyyyMMdd"),
                ReleasePeriod = pointsPoolStakeSumDto.ReleasePeriod
            };
            rewardsList.Add(rewardsEto);
        }

        return rewardsList;
    }

    private async Task<List<PointsStakeRewardsSumEto>> UpdateAddressRewardsSumAsync(PointsSnapshotIndex pointsSnapshot,
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic,
        Dictionary<string, List<PointsStakeRewardsIndex>> endedPeriodRewardsDic)
    {
        var rewardsSumList = new List<PointsStakeRewardsSumEto>();
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        foreach (var (poolIndex, pointsPoolStakeSumDto) in stakeSumDic)
        {
            var value = CheckPoints(poolIndex, pointsPoolStakeSumDto.PoolId, pointsSnapshot);

            if (value == "0")
            {
                continue;
            }


            //record the rewards of the previous day
            var id = GuidHelper.GenerateId(pointsSnapshot.Address, pointsPoolStakeSumDto.PoolId);
            var stakeAmount = decimal.Parse(pointsPoolStakeSumDto.StakeAmount);
            var totalRewards = stakeAmount == 0
                ? 0
                : Math.Floor(decimal.Parse(value) / stakeAmount * pointsPoolStakeSumDto.DailyReward *
                             100000000) / 100000000;
            var rewards = Math.Floor(totalRewards / pointsPoolStakeSumDto.ReleasePeriod * 100000000) / 100000000;

            //update the rewards sum for each address in each points pool

            var rewardsSumGrain = _clusterClient.GetGrain<IPointsStakeRewardsSumGrain>(id);
            var rewardsSumDto = await rewardsSumGrain.GetAsync();
            if (rewardsSumDto == null)
            {
                rewardsSumDto = new PointsStakeRewardsSumDto
                {
                    Id = id,
                    PoolId = pointsPoolStakeSumDto.PoolId,
                    PoolName = pointsPoolStakeSumDto.PoolName,
                    DappId = pointsPoolStakeSumDto.DappId,
                    Address = pointsSnapshot.Address,
                    CreateTime = now,
                    Rewards = rewards.ToString(CultureInfo.InvariantCulture),
                    TotalRewards = totalRewards.ToString(CultureInfo.InvariantCulture),
                    FrozenAmount = (totalRewards - rewards).ToString(CultureInfo.InvariantCulture),
                    ClaimedAmount = "0",
                    LastSettleAmount = rewards.ToString(CultureInfo.InvariantCulture),
                };
            }
            else
            {
                rewardsSumDto.UpdateTime = now;
                rewardsSumDto.Rewards =
                    (decimal.Parse(rewardsSumDto.Rewards) + rewards).ToString(CultureInfo.InvariantCulture);
                rewardsSumDto.TotalRewards =
                    (decimal.Parse(rewardsSumDto.TotalRewards) + totalRewards).ToString(CultureInfo.InvariantCulture);
                rewardsSumDto.FrozenAmount =
                    (decimal.Parse(rewardsSumDto.FrozenAmount) + totalRewards - rewards).ToString(CultureInfo
                        .InvariantCulture);
                var lastSettleAmount = decimal.Parse(rewardsSumDto.LastSettleAmount) + rewards;

                if (endedPeriodRewardsDic.TryGetValue(id, out var periodRewardsList))
                {
                    var endedPeriodRewards = periodRewardsList.Select(x => decimal.Parse(x.Rewards)).Sum();
                    lastSettleAmount -= endedPeriodRewards;
                }

                rewardsSumDto.LastSettleAmount = lastSettleAmount.ToString(CultureInfo.InvariantCulture);
            }

            var rewardsSumResult = await rewardsSumGrain.CreateOrUpdateAsync(rewardsSumDto);

            rewardsSumList.Add(
                _objectMapper.Map<PointsStakeRewardsSumDto, PointsStakeRewardsSumEto>(rewardsSumResult.Data));
        }

        return rewardsSumList;
    }

    private string CheckPoints(string index, string poolId, PointsSnapshotIndex pointsSnapshot)
    {
        if (string.IsNullOrEmpty(poolId))
        {
            return "0";
        }

        var symbolFieldName = PoolInfoConst.PoolIndexSymbolDic[index];
        var property = typeof(PointsSnapshotIndex).GetProperty(symbolFieldName);
        var value = property != null ? property.GetValue(pointsSnapshot) : null;
        return value == null ? "0" : value.ToString();
    }

    public async Task UpdatePointsPoolStakeSumAsync(Dictionary<string, PointsPoolStakeSumDto> stakeSumDic)
    {
        var stakeSumList = new List<PointsPoolStakeSumEto>();

        foreach (var (poolIndex, pointsPoolStakeSumDto) in stakeSumDic)
        {
            if (string.IsNullOrEmpty(pointsPoolStakeSumDto.PoolId))
            {
                continue;
            }

            var id = GuidHelper.GenerateId(pointsPoolStakeSumDto.PoolId, poolIndex);

            //update the staked amount for each address in each points pool
            var input = new PointsPoolStakeSumDto
            {
                Id = id,
                PoolId = pointsPoolStakeSumDto.PoolId,
                PoolName = pointsPoolStakeSumDto.PoolName,
                DappId = pointsPoolStakeSumDto.DappId,
                StakeAmount = pointsPoolStakeSumDto.StakeAmount
            };
            var recordGrain = _clusterClient.GetGrain<IPointsPoolStakeSumGrain>(id);
            var result = await recordGrain.CreateOrUpdateAsync(input);

            if (!result.Success)
            {
                _logger.LogError(
                    "update address stake amount fail, message:{message}, id:{id}",
                    result.Message, id);
                continue;
            }

            stakeSumList.Add(_objectMapper.Map<PointsPoolStakeSumDto, PointsPoolStakeSumEto>(result.Data));
        }

        var rewardsSumListEto = new PointsPoolStakeSumListEto { EventDataList = stakeSumList };
        await _distributedEventBus.PublishAsync(rewardsSumListEto);
    }
}