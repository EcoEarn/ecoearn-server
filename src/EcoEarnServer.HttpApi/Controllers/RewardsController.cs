using System.Threading.Tasks;
using EcoEarnServer.Rewards;
using EcoEarnServer.Rewards.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

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
    public async Task<PagedResultDto<RewardsListDto>> GetRewardsListAsync(GetRewardsListInput input)
    {
        return await _rewardsService.GetRewardsListAsync(input);
    }

    [HttpPost("aggregation")]
    public async Task<RewardsAggregationDto> GetRewardsAggregationAsync(GetRewardsAggregationInput input)
    {
        return await _rewardsService.GetRewardsAggregationAsync(input);
    }

    [HttpPost("withdraw/signature")]
    public async Task<RewardsSignatureDto> RewardsWithdrawSignatureAsync(RewardsSignatureInput input)
    {
        return await _rewardsService.RewardsWithdrawSignatureAsync(input);
    }

    [HttpPost("withdraw")]
    public async Task<string> RewardsWithdrawAsync(RewardsTransactionInput input)
    {
        return await _rewardsService.RewardsWithdrawAsync(input);
    }

    [HttpPost("early/stake/signature")]
    public async Task<RewardsSignatureDto> EarlyStakeSignatureAsync(RewardsSignatureInput input)
    {
        return await _rewardsService.EarlyStakeSignatureAsync(input);
    }

    [HttpPost("early/stake")]
    public async Task<string> EarlyStakeAsync(RewardsTransactionInput input)
    {
        return await _rewardsService.EarlyStakeAsync(input);
    }


    [HttpPost("add/liquidity/signature")]
    public async Task<RewardsSignatureDto> AddLiquiditySignatureAsync(RewardsSignatureInput input)
    {
        return await _rewardsService.AddLiquiditySignatureAsync(input);
    }

    [HttpPost("add/liquidity")]
    public async Task<string> AddLiquidityAsync(RewardsTransactionInput input)
    {
        return await _rewardsService.AddLiquidityAsync(input);
    }

    [HttpPost("cancel/signature")]
    public async Task<bool> CancelSignatureAsync(RewardsSignatureInput input)
    {
        return await _rewardsService.CancelSignatureAsync(input);
    }
}