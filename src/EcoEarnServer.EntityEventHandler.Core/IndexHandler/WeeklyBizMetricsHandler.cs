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

public class WeeklyBizMetricsHandler : IDistributedEventHandler<WeeklyBizMetricsListEto>,
    ITransientDependency
{
    private readonly INESTRepository<WeeklyBizMetricsIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<WeeklyBizMetricsHandler> _logger;

    public WeeklyBizMetricsHandler(INESTRepository<WeeklyBizMetricsIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<WeeklyBizMetricsHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(WeeklyBizMetricsListEto eventData)
    {
        if (eventData.EventDataList.Count == 0)
        {
            return;
        }

        try
        {
            var indexList = _objectMapper.Map<List<WeeklyBizMetricsEto>, List<WeeklyBizMetricsIndex>>(eventData.EventDataList);
            await _repository.BulkAddOrUpdateAsync(indexList);
            _logger.LogDebug("HandleEventAsync WeeklyBizMetricsListEto success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync WeeklyBizMetricsListEto fail. {Message}",
                JsonConvert.SerializeObject(eventData));
        }
    }
}