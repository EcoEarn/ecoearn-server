using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.StakingSettlePoints;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.EntityEventHandler.Core.IndexHandler;

public class StakeCountHandler : IDistributedEventHandler<StakeCountListEto>,
    ITransientDependency
{
    private readonly INESTRepository<StakeCountIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<StakeCountHandler> _logger;

    public StakeCountHandler(INESTRepository<StakeCountIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<StakeCountHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(StakeCountListEto eventData)
    {
        if (eventData.EventDataList.Count == 0)
        {
            return;
        }

        try
        {
            var indexList = _objectMapper.Map<List<StakeCountEto>, List<StakeCountIndex>>(eventData.EventDataList);
            await _repository.BulkAddOrUpdateAsync(indexList);
            _logger.LogDebug("HandleEventAsync StakeCountHandler success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync StakeCountHandler fail. {Message}",
                JsonConvert.SerializeObject(eventData));
        }
    }
}