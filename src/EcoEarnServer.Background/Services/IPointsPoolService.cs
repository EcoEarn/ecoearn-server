using System;
using System.Collections.Generic;
using System.Globalization;
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
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic, int settleRewardsBeforeDays);

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
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic, int settleRewardsBeforeDays)
    {
        foreach (var pointsSnapshotIndex in pointsSnapshots)
        {
            await UpdatePointsPoolAddressStakeAsync(pointsSnapshotIndex, stakeSumDic, settleRewardsBeforeDays);
        }
    }

    private async Task<List<PointsPoolAddressStakeEto>> UpdatePointsPoolAddressStakeSumAsync(
        PointsSnapshotIndex pointsSnapshot, Dictionary<string, PointsPoolStakeSumDto> stakeSumDic)
    {
        var stakeListEto = new List<PointsPoolAddressStakeEto>();
        foreach (var (poolIndex, pointsPoolStakeSumDto) in stakeSumDic)
        {
            var value = CheckPoints(pointsPoolStakeSumDto.PoolId, poolIndex, pointsSnapshot);

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
                StakeAmount = value.ToString(),
                CreateTime = DateTime.UtcNow.ToUtcMilliSeconds()
            };

            stakeListEto.Add(
                _objectMapper.Map<PointsPoolAddressStakeDto, PointsPoolAddressStakeEto>(poolAddressStakeDto));
        }

        return stakeListEto;
    }

    private async Task<List<PointsStakeRewardsEto>> RecordPreviousDayRewardsAsync(PointsSnapshotIndex pointsSnapshot,
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic, int settleRewardsBeforeDays)
    {
        var yesterday = DateTime.UtcNow.AddDays(settleRewardsBeforeDays).ToString("yyyyMMdd");
        var rewardsList = new List<PointsStakeRewardsEto>();
        foreach (var (poolIndex, pointsPoolStakeSumDto) in stakeSumDic)
        {
            var value = CheckPoints(pointsPoolStakeSumDto.PoolId, poolIndex, pointsSnapshot);

            if (value == "0")
            {
                continue;
            }

            //record the rewards of the previous day
            var stakeAmount = decimal.Parse(pointsPoolStakeSumDto.StakeAmount);
            var rewards = stakeAmount == 0
                ? 0
                : Math.Floor(decimal.Parse(value.ToString()) / stakeAmount * pointsPoolStakeSumDto.DailyReward *
                             100000000) / 100000000;
            var rewardsId = GuidHelper.GenerateId(pointsSnapshot.Address, poolIndex, yesterday);
            var rewardsDto = new PointsStakeRewardsDto
            {
                Id = rewardsId,
                PoolId = pointsPoolStakeSumDto.PoolId,
                PoolName = pointsPoolStakeSumDto.PoolName,
                DappId = pointsPoolStakeSumDto.DappId,
                Rewards = rewards.ToString(CultureInfo.InvariantCulture),
                Address = pointsSnapshot.Address,
                SettleDate = yesterday,
                CreateTime = DateTime.UtcNow.ToUtcMilliSeconds()
            };
            rewardsList.Add(_objectMapper.Map<PointsStakeRewardsDto, PointsStakeRewardsEto>(rewardsDto));
        }

        return rewardsList;
    }

    private async Task<List<PointsStakeRewardsSumEto>> UpdateAddressRewardsSumAsync(PointsSnapshotIndex pointsSnapshot,
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic)
    {
        var rewardsSumList = new List<PointsStakeRewardsSumEto>();
        foreach (var (poolIndex, pointsPoolStakeSumDto) in stakeSumDic)
        {
            var value = CheckPoints(pointsPoolStakeSumDto.PoolId, poolIndex, pointsSnapshot);

            if (value == "0")
            {
                continue;
            }


            //record the rewards of the previous day
            var id = GuidHelper.GenerateId(pointsSnapshot.Address, pointsPoolStakeSumDto.PoolId);
            var stakeAmount = decimal.Parse(pointsPoolStakeSumDto.StakeAmount);
            var rewards = stakeAmount == 0
                ? 0
                : Math.Floor(decimal.Parse(value) / stakeAmount * pointsPoolStakeSumDto.DailyReward *
                             100000000) / 100000000;

            //update the rewards sum for each address in each points pool
            var rewardsSumDto = new PointsStakeRewardsSumDto
            {
                Id = id,
                PoolId = pointsPoolStakeSumDto.PoolId,
                PoolName = pointsPoolStakeSumDto.PoolName,
                DappId = pointsPoolStakeSumDto.DappId,
                Rewards = rewards.ToString(CultureInfo.InvariantCulture),
                Address = pointsSnapshot.Address,
            };
            var rewardsSumGrain = _clusterClient.GetGrain<IPointsStakeRewardsSumGrain>(id);
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

    private async Task UpdatePointsPoolAddressStakeAsync(PointsSnapshotIndex pointsSnapshot,
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic, int settleRewardsBeforeDays)
    {
        try
        {
            var yesterday = DateTime.UtcNow.AddDays(settleRewardsBeforeDays).ToString("yyyyMMdd");
            var stakeListEto = new List<PointsPoolAddressStakeEto>();
            var rewardsList = new List<PointsStakeRewardsEto>();
            var rewardsSumList = new List<PointsStakeRewardsSumEto>();
            foreach (var (poolIndex, pointsPoolStakeSumDto) in stakeSumDic)
            {
                if (string.IsNullOrEmpty(pointsPoolStakeSumDto.PoolId))
                {
                    continue;
                }

                var id = GuidHelper.GenerateId(pointsSnapshot.Address, pointsPoolStakeSumDto.PoolId);
                var symbolFieldName = PoolInfoConst.PoolIndexSymbolDic[poolIndex];
                var property = typeof(PointsSnapshotIndex).GetProperty(symbolFieldName);
                var value = property != null ? property.GetValue(pointsSnapshot) : null;
                if (value == null)
                {
                    continue;
                }

                if (value.ToString() == "0")
                {
                    continue;
                }

                //update the staked amount for each address in each points pool
                var poolAddressStakeDto = new PointsPoolAddressStakeDto
                {
                    Id = id,
                    PoolId = pointsPoolStakeSumDto.PoolId,
                    PoolName = pointsPoolStakeSumDto.PoolName,
                    DappId = pointsPoolStakeSumDto.DappId,
                    Address = pointsSnapshot.Address,
                    StakeAmount = value.ToString(),
                    CreateTime = DateTime.UtcNow.ToUtcMilliSeconds()
                };

                stakeListEto.Add(
                    _objectMapper.Map<PointsPoolAddressStakeDto, PointsPoolAddressStakeEto>(poolAddressStakeDto));

                //record the rewards of the previous day

                var stakeAmount = decimal.Parse(pointsPoolStakeSumDto.StakeAmount);
                var rewards = stakeAmount == 0
                    ? 0
                    : Math.Floor(decimal.Parse(value.ToString()) / stakeAmount * pointsPoolStakeSumDto.DailyReward *
                                 100000000) / 100000000;
                var rewardsId = GuidHelper.GenerateId(pointsSnapshot.Address, poolIndex, yesterday);
                var rewardsDto = new PointsStakeRewardsDto
                {
                    Id = rewardsId,
                    PoolId = pointsPoolStakeSumDto.PoolId,
                    PoolName = pointsPoolStakeSumDto.PoolName,
                    DappId = pointsPoolStakeSumDto.DappId,
                    Rewards = rewards.ToString(CultureInfo.InvariantCulture),
                    Address = pointsSnapshot.Address,
                    SettleDate = yesterday,
                    CreateTime = DateTime.UtcNow.ToUtcMilliSeconds()
                };
                rewardsList.Add(_objectMapper.Map<PointsStakeRewardsDto, PointsStakeRewardsEto>(rewardsDto));

                //update the rewards sum for each address in each points pool
                var rewardsSumDto = new PointsStakeRewardsSumDto
                {
                    Id = id,
                    PoolId = pointsPoolStakeSumDto.PoolId,
                    PoolName = pointsPoolStakeSumDto.PoolName,
                    DappId = pointsPoolStakeSumDto.DappId,
                    Rewards = rewards.ToString(CultureInfo.InvariantCulture),
                    Address = pointsSnapshot.Address,
                };
                var rewardsSumGrain = _clusterClient.GetGrain<IPointsStakeRewardsSumGrain>(id);
                var rewardsSumResult = await rewardsSumGrain.CreateOrUpdateAsync(rewardsSumDto);

                rewardsSumList.Add(
                    _objectMapper.Map<PointsStakeRewardsSumDto, PointsStakeRewardsSumEto>(rewardsSumResult.Data));
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
        catch (Exception e)
        {
            _logger.LogError(e, "settle points rewards fail");
            await _larkAlertProvider.SendLarkFailAlertAsync(e.Message);
        }

        _logger.LogInformation("UpdatePointsPoolAddressStakeAsync end. id:{id}", pointsSnapshot.Id);
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