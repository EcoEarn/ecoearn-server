using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Ranking;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.EntityEventHandler.Core.IndexHandler;

public class PointsRankingHandler : IDistributedEventHandler<PointsRankingListEto>,
    ITransientDependency
{
    private readonly INESTRepository<PointsRankingIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<PointsRankingHandler> _logger;

    public PointsRankingHandler(INESTRepository<PointsRankingIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<PointsRankingHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(PointsRankingListEto eventData)
    {
        if (eventData.EventDataList.Count == 0)
        {
            return;
        }

        try
        {
            var indexList =
                _objectMapper.Map<List<PointsRankingEto>, List<PointsRankingIndex>>(eventData.EventDataList);
            await _repository.BulkAddOrUpdateAsync(indexList);
            _logger.LogDebug("HandleEventAsync PointsRankingHandler success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync PointsRankingHandler fail. {Message}",
                JsonConvert.SerializeObject(eventData));
        }
    }
}