using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.Rewards.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Rewards;

public class RewardsService : IRewardsService, ISingletonDependency
{
    private readonly IRewardsProvider _rewardsProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<RewardsService> _logger;

    public RewardsService(IRewardsProvider rewardsProvider, IObjectMapper objectMapper, ILogger<RewardsService> logger)
    {
        _rewardsProvider = rewardsProvider;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task<List<RewardsListDto>> GetRewardsListAsync(GetRewardsListInput input)
    {
        var rewardsIndexerList = await _rewardsProvider.GetRewardsListAsync(input);
        return _objectMapper.Map<List<RewardsListIndexerDto>, List<RewardsListDto>>(rewardsIndexerList);
    }

    public Task<RewardsAggregationDto> GetRewardsAggregationAsync()
    {
        throw new NotImplementedException();
    }
}