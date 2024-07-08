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
    private readonly IMetricsService _metricsService;
    private readonly ILogger<MetricsWorker> _logger;

    public MetricsWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<PointsSnapshotOptions> options, ILogger<MetricsWorker> logger,
        IMetricsService metricsService) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _metricsService = metricsService;
        _options = options.Value;
        timer.Period =  1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("begin execute MetricsWorker. begin time: {time}", DateTime.UtcNow);
        await _metricsService.GenerateMetricsAsync();
        _logger.LogInformation("finish execute MetricsWorker. finish time: {time}", DateTime.UtcNow);
    }
}