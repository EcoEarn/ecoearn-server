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

public class StakingPointsWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ILogger<StakingPointsWorker> _logger;
    private readonly PointsSnapshotOptions _options;

    public StakingPointsWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<PointsSnapshotOptions> options, ILogger<StakingPointsWorker> logger,
        IMetricsService metricsService) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        timer.Period = options.Value.GenerateMetricsPeriod * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("begin execute MetricsWorker. begin time: {time}", DateTime.UtcNow);
        //await _metricsService.GenerateMetricsAsync();
        _logger.LogInformation("finish execute MetricsWorker. finish time: {time}", DateTime.UtcNow);
    }
} 