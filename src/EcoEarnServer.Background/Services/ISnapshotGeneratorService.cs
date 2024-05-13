using System;
using System.Threading.Tasks;
using EcoEarnServer.Background.Dtos;
using EcoEarnServer.Grains.Grain.PointsSnapshot;
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
    Task GenerateSnapshotAsync(PointsListDto dto, string snapshotDate);
}

public class SnapshotGeneratorService : ISnapshotGeneratorService, ITransientDependency
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<SnapshotGeneratorService> _logger;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IObjectMapper _objectMapper;

    public SnapshotGeneratorService(IClusterClient clusterClient, ILogger<SnapshotGeneratorService> logger,
        IObjectMapper objectMapper, IDistributedEventBus distributedEventBus)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _objectMapper = objectMapper;
        _distributedEventBus = distributedEventBus;
    }

    [AutomaticRetry(Attempts = 20, DelaysInSeconds = new[] { 40 })]
    public async Task GenerateSnapshotAsync(PointsListDto dto, string snapshotDate)
    {
        var recordId = $"{dto.Address}-{snapshotDate}";
        try
        {
            _logger.LogInformation("begin create, recordId:{recordId}", recordId);

            var input = _objectMapper.Map<PointsListDto, PointsSnapshotDto>(dto);
            input.SnapshotDate = snapshotDate;
            var recordGrain = _clusterClient.GetGrain<IPointsSnapshotGrain>(recordId);
            var result = await recordGrain.CreateAsync(input);

            if (!result.Success)
            {
                _logger.LogError(
                    "generate snapshot record grain fail, message:{message}, recordId:{recordId}",
                    result.Message, recordId);
                return;
            }

            var recordEto = _objectMapper.Map<PointsSnapshotDto, PointsSnapshotEto>(result.Data);
            await _distributedEventBus.PublishAsync(recordEto);
            _logger.LogInformation("end generate snapshot, recordId:{recordId}", recordId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "generate snapshot record grain fail, recordId:{recordId}", recordId);
            throw;
        }
    }
}