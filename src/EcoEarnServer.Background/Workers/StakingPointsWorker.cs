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
    private readonly IStakingPointsService _stakingPointsService;

    public StakingPointsWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<PointsSnapshotOptions> options, ILogger<StakingPointsWorker> logger,
        IStakingPointsService stakingPointsService) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _stakingPointsService = stakingPointsService;
        _options = options.Value;
        timer.Period = 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("begin execute StakingPointsWorker. begin time: {time}", DateTime.UtcNow);
        await _stakingPointsService.ExecuteAsync();
        _logger.LogInformation("finish execute StakingPointsWorker. finish time: {time}", DateTime.UtcNow);
    }
}