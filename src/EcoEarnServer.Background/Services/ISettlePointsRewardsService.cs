using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Background.Provider;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Grains.Grain.PointsPool;
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.PointsStaking.Provider;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NUglify.Helpers;
using Volo.Abp;
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
    private const long DailySeconds = 86400;

    private readonly ISettlePointsRewardsProvider _settlePointsRewardsProvider;
    private readonly IPointsPoolService _pointsPoolService;
    private readonly IObjectMapper _objectMapper;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ILogger<SettlePointsRewardsService> _logger;
    private readonly IStateProvider _stateProvider;
    private readonly PointsSnapshotOptions _pointsSnapshotOptions;
    private readonly ILarkAlertProvider _larkAlertProvider;
    private readonly IPointsStakingProvider _pointsStakingProvider;

    public SettlePointsRewardsService(ISettlePointsRewardsProvider settlePointsRewardsProvider,
        IPointsPoolService pointsPoolService, IObjectMapper objectMapper, IAbpDistributedLock distributedLock,
        ILogger<SettlePointsRewardsService> logger, IStateProvider stateProvider,
        IOptionsSnapshot<PointsSnapshotOptions> pointsSnapshotOptions, ILarkAlertProvider larkAlertProvider,
        IPointsStakingProvider pointsStakingProvider)
    {
        _settlePointsRewardsProvider = settlePointsRewardsProvider;
        _pointsPoolService = pointsPoolService;
        _objectMapper = objectMapper;
        _distributedLock = distributedLock;
        _logger = logger;
        _stateProvider = stateProvider;
        _larkAlertProvider = larkAlertProvider;
        _pointsStakingProvider = pointsStakingProvider;
        _pointsSnapshotOptions = pointsSnapshotOptions.Value;
    }

    public async Task SettlePointsRewardsAsync(int settleRewardsBeforeDays)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(name: LockKeyPrefix);

        if (handle == null)
        {
            _logger.LogWarning("do not get settle lock, keys already exits.");
            return;
        }

        if (!await _stateProvider.CheckStateAsync(StateGeneratorHelper.GenerateSnapshotKey(), true))
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
            var stakeSumDic = await GetYesterdayStakeSumDic(list);
            //update the staked sum for each points pool
            if (_pointsSnapshotOptions.SettleRewards)
            {
                await PointsBatchUpdateAsync(list, stakeSumDic, settleRewardsBeforeDays);
            }

            await _pointsPoolService.UpdatePointsPoolStakeSumAsync(stakeSumDic);
            await _stateProvider.SetStateAsync(StateGeneratorHelper.GenerateSettleKey(settleRewardsBeforeDays), true);

            var larkAlertDto = BuildLarkAlertParam(list.Count, DateTime.UtcNow.ToString("yyyy-MM-dd"), true);
            await _larkAlertProvider.SendLarkAlertAsync(larkAlertDto);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "SettlePointsRewards fail.");
            await _stateProvider.SetStateAsync(StateGeneratorHelper.GenerateSettleKey(settleRewardsBeforeDays), false);
            await _larkAlertProvider.SendLarkFailAlertAsync(e.Message);
        }
    }


    private async Task PointsBatchUpdateAsync(List<PointsSnapshotIndex> snapshotList,
        Dictionary<string, PointsPoolStakeSumDto> stakeSumDic, int settleRewardsBeforeDays)
    {
        var recurCount = snapshotList.Count / _pointsSnapshotOptions.BatchSnapshotCount + 1;
        for (var i = 0; i < recurCount; i++)
        {
            var skipCount = _pointsSnapshotOptions.BatchSnapshotCount * i;
            var list = snapshotList.Skip(skipCount).Take(_pointsSnapshotOptions.BatchSnapshotCount).ToList();

            if (list.IsNullOrEmpty()) return;
            BackgroundJob.Enqueue(() =>
                _pointsPoolService.BatchUpdatePointsPoolAddressStakeAsync(list, stakeSumDic, settleRewardsBeforeDays));
            await Task.Delay(_pointsSnapshotOptions.TaskDelayMilliseconds);
        }
    }

    private async Task<Dictionary<string, PointsPoolStakeSumDto>> GetYesterdayStakeSumDic(
        List<PointsSnapshotIndex> list)
    {
        var poolOptionsDic = await GetPointsPoolInfosAsync();
        var groupedSnapshot = list.GroupBy(x => x.DappId)
            .Select(g => new
            {
                DappId = g.Key,
                Rewards = g.ToList()
            })
            .ToList();
        var poolStakeDic = new Dictionary<string, PointsPoolStakeSumDto>();
        poolOptionsDic.ForEach(entry =>
        {
            poolStakeDic[entry.Key] = _objectMapper.Map<PointsPoolInfo, PointsPoolStakeSumDto>(entry.Value);
        });

        foreach (var snapshotList in groupedSnapshot)
        {
            var firstSum = BigInteger.Zero;
            var secondSum = BigInteger.Zero;
            var thirdSum = BigInteger.Zero;
            var fourSum = BigInteger.Zero;
            var fiveSum = BigInteger.Zero;
            var sixSum = BigInteger.Zero;
            var sevenSum = BigInteger.Zero;
            var eightSum = BigInteger.Zero;
            var nineSum = BigInteger.Zero;
            var tenSum = BigInteger.Zero;
            var elevenSum = BigInteger.Zero;
            var twelveSum = BigInteger.Zero;
            var dappId = snapshotList.DappId;
            foreach (var pointsSnapshot in snapshotList.Rewards)
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
                tenSum += BigInteger.Parse(pointsSnapshot.TenSymbolAmount);
                elevenSum += BigInteger.Parse(pointsSnapshot.ElevenSymbolAmount);
                twelveSum += BigInteger.Parse(pointsSnapshot.TwelveSymbolAmount);
            }

            if (dappId == _pointsSnapshotOptions.SchrodingerDappId &&
                _pointsSnapshotOptions.SchrodingerUnBoundPointsSwitch)
            {
                try
                {
                    var unboundEvmAddressPoints = await _settlePointsRewardsProvider.GetUnboundEvmAddressPointsAsync();
                    tenSum += new BigInteger(Convert.ToDecimal(unboundEvmAddressPoints));
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "get un bound evm address amount fail.");
                    await _larkAlertProvider.SendLarkFailAlertAsync(e.Message);
                    throw new UserFriendlyException("get un bound evm address amount fail.");
                }
            }

            foreach (var pointsPoolStakeSumDto in poolStakeDic.Values.Where(x => x.DappId == dappId))
            {
                var pointsName = pointsPoolStakeSumDto.PointsName;
                if (pointsName.EndsWith("-1"))
                {
                    pointsPoolStakeSumDto.StakeAmount = firstSum.ToString();
                }
                else if (pointsName.EndsWith("-2"))
                {
                    pointsPoolStakeSumDto.StakeAmount = secondSum.ToString();
                }
                else if (pointsName.EndsWith("-3"))
                {
                    pointsPoolStakeSumDto.StakeAmount = thirdSum.ToString();
                }
                else if (pointsName.EndsWith("-4"))
                {
                    pointsPoolStakeSumDto.StakeAmount = fourSum.ToString();
                }
                else if (pointsName.EndsWith("-5"))
                {
                    pointsPoolStakeSumDto.StakeAmount = fiveSum.ToString();
                }
                else if (pointsName.EndsWith("-6"))
                {
                    pointsPoolStakeSumDto.StakeAmount = sixSum.ToString();
                }
                else if (pointsName.EndsWith("-7"))
                {
                    pointsPoolStakeSumDto.StakeAmount = sevenSum.ToString();
                }
                else if (pointsName.EndsWith("-8"))
                {
                    pointsPoolStakeSumDto.StakeAmount = eightSum.ToString();
                }
                else if (pointsName.EndsWith("-9"))
                {
                    pointsPoolStakeSumDto.StakeAmount = nineSum.ToString();
                }
                else if (pointsName.EndsWith("-10"))
                {
                    pointsPoolStakeSumDto.StakeAmount = tenSum.ToString();
                }
                else if (pointsName.EndsWith("-11"))
                {
                    pointsPoolStakeSumDto.StakeAmount = elevenSum.ToString();
                }
                else if (pointsName.EndsWith("-12"))
                {
                    pointsPoolStakeSumDto.StakeAmount = twelveSum.ToString();
                }
            }
        }

        return poolStakeDic;
    }

    private async Task<Dictionary<string, PointsPoolInfo>> GetPointsPoolInfosAsync()
    {
        var pointsPoolsIndexerDtos = await _pointsStakingProvider.GetPointsPoolsAsync("");
        return pointsPoolsIndexerDtos.ToDictionary(x => x.PointsName, x => new PointsPoolInfo
        {
            PoolId = x.PoolId,
            PoolName = x.PointsName,
            PointsName = x.PointsName,
            DappId = x.DappId,
            DailyReward = decimal.Parse((x.PointsPoolConfig.RewardPerBlock * DailySeconds).ToString()) /
                          decimal.Parse("100000000")
        });
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

        var larkAlertDto = BuildLarkAlertParam(res.Count, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        await _larkAlertProvider.SendLarkAlertAsync(larkAlertDto);
        return res;
    }

    private LarkAlertDto BuildLarkAlertParam(int count, string date, bool isSnapshotEnd = false)
    {
        var msg = isSnapshotEnd
            ? $"Points Rewards Settle End({date}).\n"
            : $"Points Rewards Settle Start({date}).\nPoints Snapshot Count: {count}. \n";
        var content = new Dictionary<string, string>()
        {
            ["text"] = msg,
        };
        return new LarkAlertDto
        {
            MsgType = LarkAlertMsgType.Text,
            Content = JsonConvert.SerializeObject(content)
        };
    }
}