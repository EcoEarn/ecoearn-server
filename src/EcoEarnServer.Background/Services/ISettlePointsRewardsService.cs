using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Background.Provider;
using EcoEarnServer.Constants;
using EcoEarnServer.Grains.Grain.PointsPool;
using EcoEarnServer.PointsSnapshot;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUglify.Helpers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Background.Services;

public interface ISettlePointsRewardsService
{
    Task SettlePointsRewardsAsync(int settleRewardsBeforeDays);
}

public class SettlePointsRewardsService : ISettlePointsRewardsService, ISingletonDependency
{
    private const string LockKeyPrefix = "EcoEarnServer:SettlePointsRewards:Lock:";

    private readonly ISettlePointsRewardsProvider _settlePointsRewardsProvider;
    private readonly PointsPoolOptions _poolOptions;
    private readonly IPointsPoolService _pointsPoolService;
    private readonly IObjectMapper _objectMapper;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ILogger<SettlePointsRewardsService> _logger;
    private readonly IStateProvider _stateProvider;

    public SettlePointsRewardsService(ISettlePointsRewardsProvider settlePointsRewardsProvider,
        IOptionsSnapshot<PointsPoolOptions> poolOptions, IPointsPoolService pointsPoolService,
        IObjectMapper objectMapper, IAbpDistributedLock distributedLock, ILogger<SettlePointsRewardsService> logger,
        IStateProvider stateProvider)
    {
        _settlePointsRewardsProvider = settlePointsRewardsProvider;
        _pointsPoolService = pointsPoolService;
        _objectMapper = objectMapper;
        _distributedLock = distributedLock;
        _logger = logger;
        _stateProvider = stateProvider;
        _poolOptions = poolOptions.Value;
    }

    public async Task SettlePointsRewardsAsync(int settleRewardsBeforeDays)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(name: LockKeyPrefix);

        if (handle == null)
        {
            _logger.LogWarning("do not get settle lock, keys already exits.");
            return;
        }

        if (!await _stateProvider.CheckStateAsync(StateGeneratorHelper.GenerateSnapshotKey()))
        {
            _logger.LogInformation("today points snapshot has not ready.");
            return;
        }
        
        if (await _stateProvider.CheckStateAsync(StateGeneratorHelper.GenerateSettleKey(settleRewardsBeforeDays)))
        {
            _logger.LogInformation("today has already settle points rewards.");
            return;
        }

        try
        {
            var list = await GetYesterdaySnapshotAsync(settleRewardsBeforeDays);
            var stakeSumDic = GetYesterdayStakeSumDic(list);
            list.ForEach(snapshot =>
                BackgroundJob.Enqueue(() =>
                    _pointsPoolService.UpdatePointsPoolAddressStakeAsync(snapshot, stakeSumDic)));
            //update the staked sum for each points pool
            await _pointsPoolService.UpdatePointsPoolStakeSumAsync(stakeSumDic);
            
            await _stateProvider.SetStateAsync(StateGeneratorHelper.GenerateSettleKey(settleRewardsBeforeDays), true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CreatePointsSnapshot fail.");
            await _stateProvider.SetStateAsync(StateGeneratorHelper.GenerateSettleKey(settleRewardsBeforeDays), false);
        }
    }

    private Dictionary<string, PointsPoolStakeSumDto> GetYesterdayStakeSumDic(
        List<PointsSnapshotIndex> list)
    {
        var poolOptionsDic = _poolOptions.PointsPoolDictionary;
        var poolStakeDic = new Dictionary<string, PointsPoolStakeSumDto>();
        poolOptionsDic.ForEach(entry =>
        {
            poolStakeDic[entry.Key] = _objectMapper.Map<PointsPoolInfo, PointsPoolStakeSumDto>(entry.Value);
        });
        var firstSum = BigInteger.Zero;
        var secondSum = BigInteger.Zero;
        var thirdSum = BigInteger.Zero;
        var fourSum = BigInteger.Zero;
        var fiveSum = BigInteger.Zero;
        var sixSum = BigInteger.Zero;
        var sevenSum = BigInteger.Zero;
        var eightSum = BigInteger.Zero;
        var nineSum = BigInteger.Zero;
        foreach (var pointsSnapshot in list)
        {
            firstSum += BigInteger.Parse(pointsSnapshot.FirstSymbolAmount);
            secondSum += BigInteger.Parse(pointsSnapshot.SecondSymbolAmount);
            thirdSum += BigInteger.Parse(pointsSnapshot.ThirdSymbolAmount);
            fourSum += BigInteger.Parse(pointsSnapshot.FourSymbolAmount);
            fiveSum += BigInteger.Parse(pointsSnapshot.FiveSymbolAmount);
            sixSum += BigInteger.Parse(pointsSnapshot.SixSymbolAmount);
            sevenSum += BigInteger.Parse(pointsSnapshot.SevenSymbolAmount);
            eightSum += BigInteger.Parse(pointsSnapshot.EightSymbolAmount);
            nineSum += BigInteger.Parse(pointsSnapshot.NineSymbolAmount);
        }

        if (poolStakeDic.TryGetValue(PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.FirstSymbolAmount)],
                out var first))
        {
            first.StakeAmount = firstSum.ToString();
        }

        if (poolStakeDic.TryGetValue(PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.ThirdSymbolAmount)],
                out var third))
        {
            third.StakeAmount = thirdSum.ToString();
        }

        if (poolStakeDic.TryGetValue(PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.FourSymbolAmount)],
                out var four))
        {
            four.StakeAmount = fourSum.ToString();
        }

        if (poolStakeDic.TryGetValue(PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.FiveSymbolAmount)],
                out var five))
        {
            five.StakeAmount = fiveSum.ToString();
        }

        if (poolStakeDic.TryGetValue(PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.SixSymbolAmount)],
                out var six))
        {
            six.StakeAmount = sixSum.ToString();
        }

        if (poolStakeDic.TryGetValue(PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.SevenSymbolAmount)],
                out var seven))
        {
            seven.StakeAmount = sevenSum.ToString();
        }

        if (poolStakeDic.TryGetValue(PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.EightSymbolAmount)],
                out var eight))
        {
            eight.StakeAmount = eightSum.ToString();
        }

        if (poolStakeDic.TryGetValue(PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.NineSymbolAmount)],
                out var nine))
        {
            nine.StakeAmount = nineSum.ToString();
        }

        return poolStakeDic;
    }

    private async Task<List<PointsSnapshotIndex>> GetYesterdaySnapshotAsync(int settleRewardsBeforeDays)
    {
        var res = new List<PointsSnapshotIndex>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<PointsSnapshotIndex> list;
        var yesterday = DateTime.UtcNow.AddDays(settleRewardsBeforeDays).ToString("yyyyMMdd");
        do
        {
            list = await _settlePointsRewardsProvider.GetSnapshotListAsync(yesterday, skipCount, maxResultCount);
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
}