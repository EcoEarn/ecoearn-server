using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Background.Dtos;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Background.Provider;
using EcoEarnServer.Common;
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.Ranking;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Background.Services;

public interface ISnapshotGeneratorService
{
    Task BatchGenerateSnapshotAsync(List<PointsListDto> list, string snapshotDate);
}

public class SnapshotGeneratorService : ISnapshotGeneratorService, ITransientDependency
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<SnapshotGeneratorService> _logger;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IObjectMapper _objectMapper;
    private readonly ILarkAlertProvider _larkAlertProvider;
    private readonly PointsSnapshotOptions _options;

    public SnapshotGeneratorService(IClusterClient clusterClient, ILogger<SnapshotGeneratorService> logger,
        IObjectMapper objectMapper, IDistributedEventBus distributedEventBus, ILarkAlertProvider larkAlertProvider,
        IOptionsSnapshot<PointsSnapshotOptions> options)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _objectMapper = objectMapper;
        _distributedEventBus = distributedEventBus;
        _larkAlertProvider = larkAlertProvider;
        _options = options.Value;
    }

    [AutomaticRetry(Attempts = 20, DelaysInSeconds = new[] { 40 })]
    public async Task BatchGenerateSnapshotAsync(List<PointsListDto> list, string snapshotDate)
    {
        try
        {
            var etoList = list.Select(dto =>
            {
                var pointsSnapshotEto = _objectMapper.Map<PointsListDto, PointsSnapshotEto>(dto);
                pointsSnapshotEto.Id = GuidHelper.GenerateId(dto.Address, dto.DappId, snapshotDate);
                pointsSnapshotEto.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();
                pointsSnapshotEto.SnapshotDate = snapshotDate;
                return pointsSnapshotEto;
            }).ToList();

            await _distributedEventBus.PublishAsync(new PointsSnapshotListEto
            {
                EventDataList = etoList
            });
            _logger.LogInformation("batch generate points snapshot count : {count} ", etoList.Count);

            await SaveRankingPoints(list);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "generate snapshot fail.");
            await _larkAlertProvider.SendLarkFailAlertAsync(e.Message);
        }
    }

    private async Task SaveRankingPoints(List<PointsListDto> list)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        var etoList = list
            .Where(x => x.DappId == _options.EcoEarnDappId)
            .Select(dto => new PointsRankingEto
            {
                Id = GuidHelper.GenerateId(dto.Address, dto.DappId),
                Address = dto.Address,
                Points = decimal.Parse(dto.FirstSymbolAmount) / decimal.Parse("100000000"),
                UpdateTime = now
            }).ToList();

        if (etoList.Count == 0)
        {
            return;
        }

        await _distributedEventBus.PublishAsync(new PointsRankingListEto
        {
            EventDataList = etoList
        });
        _logger.LogInformation("batch save ranking pointscount : {count} ", etoList.Count);
    }
}