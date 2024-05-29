using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Background.Dtos;
using EcoEarnServer.Background.Provider;
using EcoEarnServer.Common;
using EcoEarnServer.PointsSnapshot;
using Hangfire;
using Microsoft.Extensions.Logging;
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

    public SnapshotGeneratorService(IClusterClient clusterClient, ILogger<SnapshotGeneratorService> logger,
        IObjectMapper objectMapper, IDistributedEventBus distributedEventBus, ILarkAlertProvider larkAlertProvider)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _objectMapper = objectMapper;
        _distributedEventBus = distributedEventBus;
        _larkAlertProvider = larkAlertProvider;
    }

    [AutomaticRetry(Attempts = 20, DelaysInSeconds = new[] { 40 })]
    public async Task BatchGenerateSnapshotAsync(List<PointsListDto> list, string snapshotDate)
    {
        try
        {
            var etoList = list.Select(dto =>
            {
                var pointsSnapshotEto = _objectMapper.Map<PointsListDto, PointsSnapshotEto>(dto);
                pointsSnapshotEto.Id = GuidHelper.GenerateId(dto.Address, snapshotDate);
                pointsSnapshotEto.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();
                return pointsSnapshotEto;
            }).ToList();

            await _distributedEventBus.PublishAsync(new PointsSnapshotListEto
            {
                EventDataList = etoList
            });
            _logger.LogInformation("batch generate points snapshot count : {count} ", etoList.Count);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "generate snapshot fail.");
            await _larkAlertProvider.SendLarkFailAlertAsync(e.Message);
        }
    }
}