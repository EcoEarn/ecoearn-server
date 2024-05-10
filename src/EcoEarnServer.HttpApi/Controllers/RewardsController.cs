using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.Rewards;
using EcoEarnServer.Rewards.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace EcoEarnServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("RewardsController")]
[Route("api/app/rewards")]
public class RewardsController : EcoEarnServerController
{
    private readonly IRewardsService _rewardsService;

    public RewardsController(IRewardsService rewardsService)
    {
        _rewardsService = rewardsService;
    }

    [HttpPost("list")]
    public async Task<List<RewardsListDto>> GetRewardsListAsync(GetRewardsListInput input)
    {
        return await _rewardsService.GetRewardsListAsync(input);
    }

    [HttpPost("aggregation")]
    public async Task<RewardsAggregationDto> GetRewardsAggregationAsync(GetRewardsAggregationInput input)
    {
        return await _rewardsService.GetRewardsAggregationAsync(input);
    }
}