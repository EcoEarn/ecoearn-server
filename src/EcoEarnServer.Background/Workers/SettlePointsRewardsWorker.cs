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

public class SettlePointsRewardsWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly PointsSnapshotOptions _options;
    private readonly ISettlePointsRewardsService _settlePointsRewardsService;
    private readonly ILogger<SettlePointsRewardsWorker> _logger;

    public SettlePointsRewardsWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<PointsSnapshotOptions> options, ILogger<SettlePointsRewardsWorker> logger,
        ISettlePointsRewardsService settlePointsRewardsService) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _settlePointsRewardsService = settlePointsRewardsService;
        _options = options.Value;
        timer.Period = 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("begin execute SettlePointsRewardsWorker. begin time: {time}", DateTime.UtcNow);
        await _settlePointsRewardsService.SettlePointsRewardsAsync(_options.SettleRewardsBeforeDays);
        _logger.LogInformation("finish execute SettlePointsRewardsWorker. finish time: {time}", DateTime.UtcNow);
    }
}