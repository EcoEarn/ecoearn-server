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
using Microsoft.Extensions.Options;
using NUglify.Helpers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Background.Services;

public interface ISettlePointsRewardsService
{
    Task SettlePointsRewardsAsync();
}

public class SettlePointsRewardsService : ISettlePointsRewardsService, ISingletonDependency
{
    private readonly ISettlePointsRewardsProvider _settlePointsRewardsProvider;
    private readonly PointsPoolOptions _poolOptions;
    private readonly IPointsPoolService _pointsPoolService;
    private readonly IObjectMapper _objectMapper;

    public SettlePointsRewardsService(ISettlePointsRewardsProvider settlePointsRewardsProvider,
        IOptionsSnapshot<PointsPoolOptions> poolOptions, IPointsPoolService pointsPoolService,
        IObjectMapper objectMapper)
    {
        _settlePointsRewardsProvider = settlePointsRewardsProvider;
        _pointsPoolService = pointsPoolService;
        _objectMapper = objectMapper;
        _poolOptions = poolOptions.Value;
    }


    public async Task SettlePointsRewardsAsync()
    {
        var list = await GetYesterdaySnapshotAsync();
        var stakeSumDic = GetYesterdayStakeSumDic(list);
        list.ForEach(snapshot =>
            BackgroundJob.Enqueue(() =>
                _pointsPoolService.UpdatePointsPoolAddressStakeAsync(snapshot, stakeSumDic)));
        //update the staked sum for each points pool
        await _pointsPoolService.UpdatePointsPoolStakeSumAsync(stakeSumDic);
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


        poolStakeDic[PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.FirstSymbolAmount)]].StakeAmount =
            firstSum.ToString();
        poolStakeDic[PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.SecondSymbolAmount)]].StakeAmount =
            secondSum.ToString();
        poolStakeDic[PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.ThirdSymbolAmount)]].StakeAmount =
            thirdSum.ToString();
        poolStakeDic[PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.FourSymbolAmount)]].StakeAmount =
            fourSum.ToString();
        poolStakeDic[PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.FiveSymbolAmount)]].StakeAmount =
            fiveSum.ToString();
        poolStakeDic[PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.SixSymbolAmount)]].StakeAmount =
            sixSum.ToString();
        poolStakeDic[PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.SevenSymbolAmount)]].StakeAmount =
            sevenSum.ToString();
        poolStakeDic[PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.EightSymbolAmount)]].StakeAmount =
            eightSum.ToString();
        poolStakeDic[PoolInfoConst.SymbolPoolIndexDic[nameof(PointsSnapshotIndex.NineSymbolAmount)]].StakeAmount =
            nineSum.ToString();

        return poolStakeDic;
    }

    private async Task<List<PointsSnapshotIndex>> GetYesterdaySnapshotAsync()
    {
        var res = new List<PointsSnapshotIndex>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<PointsSnapshotIndex> list;
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd");
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