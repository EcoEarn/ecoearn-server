using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.ExceptionHandle;
using EcoEarnServer.StakingSettlePoints;
using Microsoft.Extensions.Logging;
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService),
        MethodName = nameof(ExceptionHandlingService.HandleException), LogOnly = true,
        LogTargets = ["eventData"], Message = "AddressStakingSettlePointsHandler error")]
    public async Task HandleEventAsync(AddressStakingSettlePointsListEto eventData)
    {
        if (eventData.EventDataList.Count == 0)
        {
            return;
        }

        var indexList =
            _objectMapper.Map<List<AddressStakingSettlePointsEto>, List<AddressStakingSettlePointsIndex>>(
                eventData.EventDataList);
        await _repository.BulkAddOrUpdateAsync(indexList);
        _logger.LogDebug("HandleEventAsync AddressStakingSettlePointsHandler success.");
    }
}