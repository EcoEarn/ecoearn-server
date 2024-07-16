using System.Collections.Generic;
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
[Route("api/app/farm")]
public class FarmController : EcoEarnServerController
{
    private readonly IFarmService _farmService;

    public FarmController(IFarmService farmService)
    {
        _farmService = farmService;
    }
    
    [HttpPost("my/liquidity")]
    public async Task<List<LiquidityInfoDto>> GetMyLiquidityListAsync(GetMyLiquidityListInput input)
    {
        return await _farmService.GetMyLiquidityListAsync(input);
    }
    
    [HttpPost("market")]
    public async Task<List<MarketLiquidityInfoDto>> GetMarketLiquidityListAsync(GetMyLiquidityListInput input)
    {
        return await _farmService.GetMarketLiquidityListAsync(input);
    }
    
    [HttpPost("liquidity/list")]
    public async Task<PagedResultDto<LiquidityInfoListDto>> GetLiquidityListAsync(GetLiquidityListInput input)
    {
        return await _farmService.GetLiquidityListAsync(input);
    }
}