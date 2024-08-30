using EcoEarnServer.Background.Options;
using Hangfire;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Services;

public interface IInitJobsService
{
    void InitRecurringJob();
}

public class InitJobsService : IInitJobsService, ISingletonDependency
{
    private readonly IRecurringJobManager _recurringJobs;
    private readonly PointsSnapshotOptions _options;

    public InitJobsService(IRecurringJobManager recurringJobs, IOptionsSnapshot<PointsSnapshotOptions> options)
    {
        _recurringJobs = recurringJobs;
        _options = options.Value;
    }

    public void InitRecurringJob()
    {
        _recurringJobs.AddOrUpdate<IPointsSnapshotService>("IPointsSnapshotService",
            x => x.ExecuteAsync(), _options.CreateSnapshotCorn);
        _recurringJobs.AddOrUpdate<IWeeklyMetricsService>("IWeeklyMetricsService",
            x => x.ExecuteAsync(), _options.WeeklyMetricsCorn);
    }
}