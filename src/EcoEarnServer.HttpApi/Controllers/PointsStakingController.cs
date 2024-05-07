using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.PointsStaking;
using EcoEarnServer.PointsStaking.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace EcoEarnServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("PointsStakingController")]
[Route("api/app/points/staking")]
public class PointsStakingController : EcoEarnServerController
{
    private readonly IPointsStakingService _pointsStakingService;

    public PointsStakingController(IPointsStakingService pointsStakingService)
    {
        _pointsStakingService = pointsStakingService;
    }

    [HttpGet("items")]
    public async Task<List<ProjectItemListDto>> GetProjectItemListAsync()
    {
        return await _pointsStakingService.GetProjectItemListAsync();
    }

    [HttpPost("pools")]
    public async Task<List<PointsPoolsDto>> GetPointsPoolsAsync(GetPointsPoolsInput input)
    {
        return await _pointsStakingService.GetPointsPoolsAsync(input);
    }

    [HttpPost("claim/signature")]
    public async Task<ClaimAmountSignatureDto> ClaimAmountSignatureAsync(ClaimAmountSignatureInput input)
    {
        return await _pointsStakingService.ClaimAmountSignatureAsync(input);
    }

    [HttpPost("claim")]
    public async Task<string> ClaimAsync(PointsClaimInput input)
    {
        return await _pointsStakingService.ClaimAsync(input);
    }
    
    [HttpPost("early/stake/info")]
    public async Task<EarlyStakeInfoDto> GetEarlyStakeInfoAsync(GetEarlyStakeInfoInput input)
    {
        return await _pointsStakingService.GetEarlyStakeInfoAsync(input);
    }
}