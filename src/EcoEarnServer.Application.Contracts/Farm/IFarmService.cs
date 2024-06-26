using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.Farm.Dtos;

namespace EcoEarnServer.Farm;

public interface IFarmService
{
    Task<List<LiquidityInfoDto>> GetMyLiquidityListAsync(GetMyLiquidityListInput input);
    Task<List<MarketLiquidityInfoDto>> GetMarketLiquidityListAsync(GetMyLiquidityListInput input);
}