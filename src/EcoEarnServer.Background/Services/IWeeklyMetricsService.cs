using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Background.Provider;
using EcoEarnServer.Common;
using EcoEarnServer.Metrics;
using EcoEarnServer.TransactionRecord;
using Microsoft.Extensions.Logging;
using Nest;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;

namespace EcoEarnServer.Background.Services;

public interface IWeeklyMetricsService
{
    Task ExecuteAsync();
}

public class WeeklyMetricsService : IWeeklyMetricsService, ISingletonDependency
{
    private const string LockKeyPrefix = "EcoEarnServer:WeeklyMetrics:Lock:";

    private readonly INESTRepository<BizMetricsIndex, string> _bizMetricsRepository;
    private readonly INESTRepository<TransactionRecordIndex, string> _transactionRecordRepository;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILogger<WeeklyMetricsService> _logger;
    private readonly IStateProvider _stateProvider;
    private readonly ILarkAlertProvider _larkAlertProvider;

    public WeeklyMetricsService(INESTRepository<BizMetricsIndex, string> bizMetricsRepository,
        INESTRepository<TransactionRecordIndex, string> transactionRecordRepository,
        IAbpDistributedLock distributedLock, IDistributedEventBus distributedEventBus,
        ILogger<WeeklyMetricsService> logger, IStateProvider stateProvider, ILarkAlertProvider larkAlertProvider)
    {
        _bizMetricsRepository = bizMetricsRepository;
        _transactionRecordRepository = transactionRecordRepository;
        _distributedLock = distributedLock;
        _distributedEventBus = distributedEventBus;
        _logger = logger;
        _stateProvider = stateProvider;
        _larkAlertProvider = larkAlertProvider;
    }

    public async Task ExecuteAsync()
    {
        await using var handle = await _distributedLock.TryAcquireAsync(name: LockKeyPrefix);

        if (handle == null)
        {
            _logger.LogWarning("do not get lock, keys already exits.");
            return;
        }

        if (await _stateProvider.CheckStateAsync(StateGeneratorHelper.GenerateWeeklyMetricsKey()))
        {
            _logger.LogInformation("has already generate weekly metrics.");
            return;
        }

        try
        {
            await GenerateWeeklyMetricsAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CreatePointsSnapshot fail.");
            await _stateProvider.SetStateAsync(StateGeneratorHelper.GenerateWeeklyMetricsKey(), false);
            await _larkAlertProvider.SendLarkFailAlertAsync("generate weekly metrics fail.");
        }

        await _stateProvider.SetStateAsync(StateGeneratorHelper.GenerateSnapshotKey(), true, 169);
    }

    private async Task GenerateWeeklyMetricsAsync()
    {
        var now = DateTime.UtcNow;
        var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).ToUtcMilliSeconds();
        var nowDate = now.ToString("yyyy-MM-dd");

        var (startTime, endTime) = GetDateRange();
        var lastWeekPlatformEarning = await GetLastWeekPlatformEarningAsync(startTime, endTime);
        var lastWeekDau = await GetLastWeekDauAsync(startTime, endTime);
        var lastWeekRegisters = await GetLastWeekRegistersAsync(startTime, endTime);
        var lastWeekTvl = await GetLastWeekTvlAsync(startTime, endTime);
        var earningUsdAmountEto = new WeeklyBizMetricsEto
        {
            Id = GuidHelper.GenerateId(nowDate, BizType.PlatformStakedUsdAmount.ToString()),
            BizNumber = double.Parse(lastWeekPlatformEarning),
            WeeklyBizType = WeeklyBizType.EarningUsdAmount,
            CreateTime = today
        };

        var dauEto = new WeeklyBizMetricsEto
        {
            BizNumber = double.Parse(lastWeekDau),
            WeeklyBizType = WeeklyBizType.Dau,
            CreateTime = today
        };
        var registerAvgEto = new WeeklyBizMetricsEto
        {
            BizNumber = double.Parse(lastWeekRegisters),
            WeeklyBizType = WeeklyBizType.RegisterAvg,
            CreateTime = today
        };
        var tvlEto = new WeeklyBizMetricsEto
        {
            BizNumber = double.Parse(lastWeekTvl.Item1),
            WeeklyBizType = WeeklyBizType.Tvl,
            CreateTime = today
        };
        var tvlGrowthEto = new WeeklyBizMetricsEto
        {
            BizNumber = double.Parse(lastWeekTvl.Item2),
            WeeklyBizType = WeeklyBizType.TvlGrowth,
            CreateTime = today
        };

        var etos = new List<WeeklyBizMetricsEto>
        {
            earningUsdAmountEto,
            dauEto,
            registerAvgEto,
            tvlEto,
            tvlGrowthEto
        };

        await _distributedEventBus.PublishAsync(new WeeklyBizMetricsListEto()
        {
            EventDataList = etos
        });
    }

    private async Task<string> GetLastWeekRegistersAsync(long startTime, long endTime)
    {
        var res = new List<TransactionRecordIndex>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<TransactionRecordIndex> list;
        var mustQuery = new List<Func<QueryContainerDescriptor<TransactionRecordIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.IsFirstTransaction).Value(true)));
        mustQuery.Add(q => q.LongRange(i => i.Field(f => f.CreateTime).LessThan(endTime)));
        mustQuery.Add(q => q.LongRange(i => i.Field(f => f.CreateTime).GreaterThanOrEquals(startTime)));
        QueryContainer Filter(QueryContainerDescriptor<TransactionRecordIndex> f) => f.Bool(b => b.Must(mustQuery));
        do
        {
            var result =
                await _transactionRecordRepository.GetListAsync(Filter, skip: skipCount, limit: maxResultCount);
            list = result.Item2;
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < maxResultCount)
            {
                break;
            }

            skipCount += count;
        } while (!list.IsNullOrEmpty());

        var registerSum = res.Select(x => x.Address).Distinct().Count();

        return (decimal.Parse(registerSum.ToString()) / decimal.Parse("7")).ToString(CultureInfo.InvariantCulture);
    }

    private async Task<string> GetLastWeekDauAsync(long startTime, long endTime)
    {
        var res = new List<TransactionRecordIndex>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<TransactionRecordIndex> list;
        var mustQuery = new List<Func<QueryContainerDescriptor<TransactionRecordIndex>, QueryContainer>>();
        mustQuery.Add(q => q.LongRange(i => i.Field(f => f.CreateTime).LessThan(endTime)));
        mustQuery.Add(q => q.LongRange(i => i.Field(f => f.CreateTime).GreaterThanOrEquals(startTime)));
        QueryContainer Filter(QueryContainerDescriptor<TransactionRecordIndex> f) => f.Bool(b => b.Must(mustQuery));
        do
        {
            var result =
                await _transactionRecordRepository.GetListAsync(Filter, skip: skipCount, limit: maxResultCount);
            list = result.Item2;
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < maxResultCount)
            {
                break;
            }

            skipCount += count;
        } while (!list.IsNullOrEmpty());

        var groupedRecords = res
            .GroupBy(record => DateTimeOffset.FromUnixTimeMilliseconds(record.CreateTime).Date)
            .ToDictionary(g => g.Key, g => g.ToList());
        var dauSum = groupedRecords.Select(x => x.Value.Select(i => i.Address).Distinct().Count()).Sum();
        return (decimal.Parse(dauSum.ToString()) / decimal.Parse("7")).ToString(CultureInfo.InvariantCulture);
    }

    private async Task<string> GetLastWeekPlatformEarningAsync(long startTime, long endTime)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<BizMetricsIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.BizType).Value(BizType.PlatformEarning)));
        mustQuery.Add(q => q.LongRange(i => i.Field(f => f.CreateTime).LessThan(endTime)));
        mustQuery.Add(q => q.LongRange(i => i.Field(f => f.CreateTime).GreaterThanOrEquals(startTime)));

        QueryContainer Filter(QueryContainerDescriptor<BizMetricsIndex> f) => f.Bool(b => b.Must(mustQuery));
        var res = await _bizMetricsRepository.GetListAsync(Filter);
        var max = res.Item2.Select(x => x.BizNumber).Max();
        return max.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<(string, string)> GetLastWeekTvlAsync(long startTime, long endTime)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<BizMetricsIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.BizType).Value(BizType.PlatformStakedUsdAmount)));
        mustQuery.Add(q => q.LongRange(i => i.Field(f => f.CreateTime).LessThan(endTime)));
        mustQuery.Add(q => q.LongRange(i => i.Field(f => f.CreateTime).GreaterThanOrEquals(startTime)));

        QueryContainer Filter(QueryContainerDescriptor<BizMetricsIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _bizMetricsRepository.GetListAsync(Filter);
        var min = res.Item2.MinBy(x => x.CreateTime);
        var max = res.Item2.MaxBy(x => x.CreateTime);
        var subAmount = max.BizNumber - min.BizNumber;
        return (max.BizNumber.ToString(CultureInfo.InvariantCulture), subAmount.ToString(CultureInfo.InvariantCulture));
    }

    private static (long, long) GetDateRange()
    {
        var today = DateTime.UtcNow;
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
        var thisSaturday = today.Date.AddDays(daysUntilSaturday);
        var lastSaturday = thisSaturday.AddDays(-7);
        return (thisSaturday.ToUtcMilliSeconds(), lastSaturday.ToUtcMilliSeconds());
    }
}