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

public class PointsPoolAddressStakeHandler : IDistributedEventHandler<PointsPoolAddressStakeListEto>,
    ITransientDependency
{
    private readonly INESTRepository<PointsPoolAddressStakeIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<PointsPoolAddressStakeHandler> _logger;

    public PointsPoolAddressStakeHandler(INESTRepository<PointsPoolAddressStakeIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<PointsPoolAddressStakeHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(PointsPoolAddressStakeListEto eventData)
    {
        try
        {
            var indexList = _objectMapper.Map<List<PointsPoolAddressStakeEto>, List<PointsPoolAddressStakeIndex>>(eventData.EventDataList);
            await _repository.BulkAddOrUpdateAsync(indexList);
            _logger.LogDebug("HandleEventAsync PointsPoolAddressStakeEto success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync PointsPoolAddressStakeEto fail. {Message}", JsonConvert.SerializeObject(eventData));
        }
    }
}