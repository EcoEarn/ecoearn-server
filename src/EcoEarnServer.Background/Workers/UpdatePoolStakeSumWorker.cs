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

public class UpdatePoolStakeSumWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly PointsSnapshotOptions _options;
    private readonly IUpdatePoolStakeSumService _updatePoolStakeSumService;
    private readonly ILogger<UpdatePoolStakeSumWorker> _logger;

    public UpdatePoolStakeSumWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<PointsSnapshotOptions> options, ILogger<UpdatePoolStakeSumWorker> logger,
        IUpdatePoolStakeSumService updatePoolStakeSumService) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _updatePoolStakeSumService = updatePoolStakeSumService;
        _options = options.Value;
        timer.Period = options.Value.UpdatePoolStakeSumPeriod * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("begin execute UpdatePoolStakeSumWorker. begin time: {time}", DateTime.UtcNow);
        await _updatePoolStakeSumService.UpdatePoolStakeSumAsync(_options.UpdatePoolStakeSumWorkerDelayPeriod);
        _logger.LogInformation("finish execute UpdatePoolStakeSumWorker. finish time: {time}", DateTime.UtcNow);
    }
}