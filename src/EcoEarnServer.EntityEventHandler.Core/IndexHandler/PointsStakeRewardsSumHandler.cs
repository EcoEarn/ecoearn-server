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

public class PointsStakeRewardsSumHandler : IDistributedEventHandler<PointsStakeRewardsSumListEto>, ITransientDependency
{
    private readonly INESTRepository<PointsStakeRewardsSumIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<PointsStakeRewardsSumHandler> _logger;

    public PointsStakeRewardsSumHandler(INESTRepository<PointsStakeRewardsSumIndex, string> repository,
        IObjectMapper objectMapper,
        ILogger<PointsStakeRewardsSumHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(PointsStakeRewardsSumListEto eventData)
    {
        // try
        // {
        //     var indexList = _objectMapper.Map<List<PointsStakeRewardsSumEto>, List<PointsStakeRewardsSumIndex>>(eventData.EventDataList);
        //     await _repository.BulkAddOrUpdateAsync(indexList);
        //     _logger.LogDebug("HandleEventAsync PointsStakeRewardsSumHandler success.");
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "HandleEventAsync PointsStakeRewardsSumHandler fail. {Message}", JsonConvert.SerializeObject(eventData));
        // }
    }
}