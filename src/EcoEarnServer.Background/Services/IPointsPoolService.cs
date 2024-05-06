using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
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
    Task UpdatePointsPoolAddressStakeAsync(PointsSnapshotIndex pointsSnapshot,
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic);

    Task UpdatePointsPoolStakeSumAsync(Dictionary<string, PointsPoolStakeSumDto> stakeSumDic);
}

public class PointsPoolService : IPointsPoolService, ISingletonDependency
{
    private readonly IClusterClient _clusterClient;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILogger<PointsPoolService> _logger;
    private readonly IObjectMapper _objectMapper;

    public PointsPoolService(IClusterClient clusterClient, IDistributedEventBus distributedEventBus,
        ILogger<PointsPoolService> logger, IObjectMapper objectMapper)
    {
        _clusterClient = clusterClient;
        _distributedEventBus = distributedEventBus;
        _logger = logger;
        _objectMapper = objectMapper;
    }

    [AutomaticRetry(Attempts = 20, DelaysInSeconds = new[] { 40 })]
    public async Task UpdatePointsPoolAddressStakeAsync(PointsSnapshotIndex pointsSnapshot,
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic)
    {
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd");
        var stakeListEto = new List<PointsPoolAddressStakeEto>();
        var rewardsList = new List<PointsStakeRewardsEto>();
        var rewardsSumList = new List<PointsStakeRewardsSumEto>();
        foreach (var (poolIndex, pointsPoolStakeSumDto) in stakeSumDic)
        {
            var id = GuidHelper.GenerateId(pointsSnapshot.Address, pointsPoolStakeSumDto.PoolId);
            var symbolFieldName = PoolInfoConst.PoolIndexSymbolDic[poolIndex];
            var property = typeof(PointsSnapshotIndex).GetProperty(symbolFieldName);
            var value = property != null ? property.GetValue(pointsSnapshot) : null;
            if (value == null)
            {
                _logger.LogError("get address stake amount fail, id: {id}", id);
                continue;
            }

            //update the staked amount for each address in each points pool
            var input = new PointsPoolAddressStakeDto
            {
                Id = id,
                PoolId = pointsPoolStakeSumDto.PoolId,
                PoolName = pointsPoolStakeSumDto.PoolName,
                DappId = pointsPoolStakeSumDto.DappId,
                StakeAmount = value.ToString()
            };
            var recordGrain = _clusterClient.GetGrain<IPointsPoolAddressStakeGrain>(id);
            var result = await recordGrain.CreateOrUpdateAsync(input);

            if (!result.Success)
            {
                _logger.LogError(
                    "update address stake amount fail, message:{message}, id:{id}",
                    result.Message, id);
                return;
            }

            stakeListEto.Add(_objectMapper.Map<PointsPoolAddressStakeDto, PointsPoolAddressStakeEto>(result.Data));

            //record the rewards of the previous day
            var rewards = BigInteger.Parse(value.ToString()) / BigInteger.Parse(pointsPoolStakeSumDto.StakeAmount);
            var rewardsId = GuidHelper.GenerateId(pointsSnapshot.Address, poolIndex, yesterday);
            var rewardsDto = new PointsStakeRewardsDto
            {
                Id = rewardsId,
                PoolId = pointsPoolStakeSumDto.PoolId,
                PoolName = pointsPoolStakeSumDto.PoolName,
                DappId = pointsPoolStakeSumDto.DappId,
                Rewards = rewards.ToString(),
                SettleDate = yesterday
            };
            var pointsStakeRewardsGrain = _clusterClient.GetGrain<IPointsStakeRewardsGrain>(rewardsId);
            var rewardsResult = await pointsStakeRewardsGrain.CreateOrUpdateAsync(rewardsDto);

            if (!rewardsResult.Success)
            {
                _logger.LogError(
                    "update address stake amount fail, message:{message}, rewardsId: {rewardsId}",
                    result.Message, rewardsId);
                return;
            }

            rewardsList.Add(_objectMapper.Map<PointsStakeRewardsDto, PointsStakeRewardsEto>(rewardsResult.Data));

            //update the rewards sum for each address in each points pool
            var rewardsSumDto = new PointsStakeRewardsSumDto()
            {
                Id = id,
                PoolId = pointsPoolStakeSumDto.PoolId,
                PoolName = pointsPoolStakeSumDto.PoolName,
                DappId = pointsPoolStakeSumDto.DappId,
                Rewards = rewards.ToString(),
            };
            var rewardsSumGrain = _clusterClient.GetGrain<IPointsStakeRewardsSumGrain>(rewardsId);
            var rewardsSumResult = await rewardsSumGrain.CreateOrUpdateAsync(rewardsSumDto);

            if (!rewardsSumResult.Success)
            {
                _logger.LogError(
                    "update address stake amount sum fail, message:{message}, rewardsId: {rewardsId}",
                    result.Message, rewardsId);
                return;
            }

            rewardsSumList.Add(
                _objectMapper.Map<PointsStakeRewardsSumDto, PointsStakeRewardsSumEto>(rewardsSumResult.Data));
        }

        var pointsPoolAddressStakeListEto = new PointsPoolAddressStakeListEto { EventDataList = stakeListEto };
        await _distributedEventBus.PublishAsync(pointsPoolAddressStakeListEto, false, false);

        var rewardsListEto = new PointsStakeRewardsListEto { EventDataList = rewardsList };
        await _distributedEventBus.PublishAsync(rewardsListEto, false, false);

        var rewardsSumListEto = new PointsStakeRewardsSumListEto { EventDataList = rewardsSumList };
        await _distributedEventBus.PublishAsync(rewardsSumListEto, false, false);
    }

    public async Task UpdatePointsPoolStakeSumAsync(Dictionary<string, PointsPoolStakeSumDto> stakeSumDic)
    {
        var stakeSumList = new List<PointsPoolStakeSumEto>();

        foreach (var (poolIndex, pointsPoolStakeSumDto) in stakeSumDic)
        {
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
                return;
            }

            stakeSumList.Add(_objectMapper.Map<PointsPoolStakeSumDto, PointsPoolStakeSumEto>(result.Data));
        }

        var rewardsSumListEto = new PointsPoolStakeSumListEto { EventDataList = stakeSumList };
        await _distributedEventBus.PublishAsync(rewardsSumListEto, false, false);
    }
}