using System.Threading.Tasks;
using EcoEarnServer.Farm.Dtos;
using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Farm;

public interface IFarmService
{
    Task<PagedResultDto<LiquidityInfoDto>> GetMyLiquidityListAsync(GetMyLiquidityListInput input);
}