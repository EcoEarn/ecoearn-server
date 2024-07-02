using System;
using System.Threading.Tasks;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Background.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace EcoEarnServer.Background.Workers;

public class MetricsWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly PointsSnapshotOptions _options;
    private readonly IPointsSnapshotService _pointsSnapshotService;
    private readonly ILogger<MetricsWorker> _logger;

    public MetricsWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<PointsSnapshotOptions> options, IPointsSnapshotService pointsSnapshotService,
        ILogger<MetricsWorker> logger) : base(timer,
        serviceScopeFactory)
    {
        _pointsSnapshotService = pointsSnapshotService;
        _logger = logger;
        _options = options.Value;
        timer.Period = 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("begin execute PointsSnapshotWorker. begin time: {time}", DateTime.UtcNow);
        await _pointsSnapshotService.ExecuteAsync();
        _logger.LogInformation("finish execute PointsSnapshotWorker. finish time: {time}", DateTime.UtcNow);
    }
}