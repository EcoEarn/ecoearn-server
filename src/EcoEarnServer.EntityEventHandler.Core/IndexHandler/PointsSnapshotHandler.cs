using System;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.PointsSnapshot;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.EntityEventHandler.Core.IndexHandler;

public class PointsSnapshotHandler : IDistributedEventHandler<PointsSnapshotEto>, ITransientDependency
{
    private readonly INESTRepository<PointsSnapshotIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<PointsSnapshotHandler> _logger;

    public PointsSnapshotHandler(INESTRepository<PointsSnapshotIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<PointsSnapshotHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(PointsSnapshotEto eventData)
    {
        try
        {
            var index = _objectMapper.Map<PointsSnapshotEto, PointsSnapshotIndex>(eventData);
            await _repository.AddOrUpdateAsync(index);
            _logger.LogDebug("HandleEventAsync PointsSnapshotEto success. {id}", index.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync PointsSnapshotEto fail. {Message}",
                JsonConvert.SerializeObject(eventData));
        }
    }
}