using System.Threading.Tasks;
using EcoEarnServer.Farm;
using EcoEarnServer.Farm.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("RewardsController")]
[Route("api/app/farm/my/liquidity")]
public class FarmController : EcoEarnServerController
{
    private readonly IFarmService _farmService;

    public FarmController(IFarmService farmService)
    {
        _farmService = farmService;
    }
    
    [HttpPost("my/liquidity")]
    public async Task<PagedResultDto<LiquidityInfoDto>> GetMyLiquidityListAsync(GetMyLiquidityListInput input)
    {
        return await _farmService.GetMyLiquidityListAsync(input);
    }
}