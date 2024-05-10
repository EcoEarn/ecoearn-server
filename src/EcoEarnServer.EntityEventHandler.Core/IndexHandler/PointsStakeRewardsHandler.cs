using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.PointsStakeRewards;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.EntityEventHandler.Core.IndexHandler;

public class PointsStakeRewardsHandler : IDistributedEventHandler<PointsStakeRewardsListEto>, ITransientDependency
{
    private readonly INESTRepository<PointsStakeRewardsIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<PointsStakeRewardsHandler> _logger;

    public PointsStakeRewardsHandler(INESTRepository<PointsStakeRewardsIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<PointsStakeRewardsHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(PointsStakeRewardsListEto eventData)
    {
        try
        {
            var indexList = _objectMapper.Map<List<PointsStakeRewardsEto>, List<PointsStakeRewardsIndex>>(eventData.EventDataList);
            await _repository.BulkAddOrUpdateAsync(indexList);
            _logger.LogDebug("HandleEventAsync PointsStakeRewardsHandler success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync PointsStakeRewardsHandler fail. {Message}", JsonConvert.SerializeObject(eventData));
        }
    }
}