using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.PointsPool;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.EntityEventHandler.Core.IndexHandler;

public class PointsPoolStakeSumHandler : IDistributedEventHandler<PointsPoolStakeSumListEto>, ITransientDependency
{
    private readonly INESTRepository<PointsPoolStakeSumIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<PointsPoolStakeSumHandler> _logger;

    public PointsPoolStakeSumHandler(INESTRepository<PointsPoolStakeSumIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<PointsPoolStakeSumHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(PointsPoolStakeSumListEto eventData)
    {
        if (eventData.EventDataList.Count == 0)
        {
            return;
        }
        try
        {
            var indexList = _objectMapper.Map<List<PointsPoolStakeSumEto>, List<PointsPoolStakeSumIndex>>(eventData.EventDataList);
            await _repository.BulkAddOrUpdateAsync(indexList);
            _logger.LogDebug("HandleEventAsync PointsPoolStakeSumHandler success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync PointsPoolStakeSumHandler fail. {Message}", JsonConvert.SerializeObject(eventData));
        }
    }
}