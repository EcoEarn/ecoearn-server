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

public class AddressStakingSettlePointsHandler : IDistributedEventHandler<AddressStakingSettlePointsListEto>,
    ITransientDependency
{
    private readonly INESTRepository<AddressStakingSettlePointsIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<AddressStakingSettlePointsHandler> _logger;

    public AddressStakingSettlePointsHandler(INESTRepository<AddressStakingSettlePointsIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<AddressStakingSettlePointsHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(AddressStakingSettlePointsListEto eventData)
    {
        if (eventData.EventDataList.Count == 0)
        {
            return;
        }

        try
        {
            var indexList =
                _objectMapper.Map<List<AddressStakingSettlePointsEto>, List<AddressStakingSettlePointsIndex>>(
                    eventData.EventDataList);
            await _repository.BulkAddOrUpdateAsync(indexList);
            _logger.LogDebug("HandleEventAsync AddressStakingSettlePointsHandler success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync AddressStakingSettlePointsHandler fail. {Message}",
                JsonConvert.SerializeObject(eventData));
        }
    }
}