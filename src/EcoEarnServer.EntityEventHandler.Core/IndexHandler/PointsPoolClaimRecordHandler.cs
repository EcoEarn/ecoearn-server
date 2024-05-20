using System;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.PointsPool;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.EntityEventHandler.Core.IndexHandler;

public class PointsPoolClaimRecordHandler : IDistributedEventHandler<PointsPoolClaimRecordEto>,
    ITransientDependency
{
    private readonly INESTRepository<PointsPoolClaimRecordIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<PointsPoolClaimRecordHandler> _logger;

    public PointsPoolClaimRecordHandler(INESTRepository<PointsPoolClaimRecordIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<PointsPoolClaimRecordHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(PointsPoolClaimRecordEto eventData)
    {
        try
        {
            var indexList = _objectMapper.Map<PointsPoolClaimRecordEto, PointsPoolClaimRecordIndex>(eventData);
            await _repository.AddOrUpdateAsync(indexList);
            _logger.LogDebug("HandleEventAsync PointsPoolClaimRecordHandler success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync PointsPoolClaimRecordHandler fail. {Message}",
                JsonConvert.SerializeObject(eventData));
        }
    }
}