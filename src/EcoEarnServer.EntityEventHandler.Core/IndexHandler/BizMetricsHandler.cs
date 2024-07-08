using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Metrics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.EntityEventHandler.Core.IndexHandler;

public class BizMetricsHandler : IDistributedEventHandler<BizMetricsListEto>,
    ITransientDependency
{
    private readonly INESTRepository<BizMetricsIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<BizMetricsHandler> _logger;

    public BizMetricsHandler(INESTRepository<BizMetricsIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<BizMetricsHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(BizMetricsListEto eventData)
    {
        if (eventData.EventDataList.Count == 0)
        {
            return;
        }

        try
        {
            var indexList = _objectMapper.Map<List<BizMetricsEto>, List<BizMetricsIndex>>(eventData.EventDataList);
            await _repository.BulkAddOrUpdateAsync(indexList);
            _logger.LogDebug("HandleEventAsync BizMetricsListEto success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync BizMetricsListEto fail. {Message}",
                JsonConvert.SerializeObject(eventData));
        }
    }
}