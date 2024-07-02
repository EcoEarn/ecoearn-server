using System;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Rewards;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.EntityEventHandler.Core.IndexHandler;

public class RewardOperationRecordHandler : IDistributedEventHandler<RewardOperationRecordEto>, ITransientDependency
{
    private readonly INESTRepository<RewardOperationRecordIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<RewardOperationRecordHandler> _logger;

    public RewardOperationRecordHandler(INESTRepository<RewardOperationRecordIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<RewardOperationRecordHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(RewardOperationRecordEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        try
        {
            var rewardOperationRecordIndex =
                _objectMapper.Map<RewardOperationRecordEto, RewardOperationRecordIndex>(eventData);
            await _repository.AddOrUpdateAsync(rewardOperationRecordIndex);
            _logger.LogDebug("HandleEventAsync RewardOperationRecordHandler success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync RewardOperationRecordHandler fail. {Message}",
                JsonConvert.SerializeObject(eventData));
        }
    }
}