using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Farm.Dtos;

public class GetMyLiquidityListInput
{
    public string Address { get; set; }
}

public class GetLiquidityListInput : PagedResultRequestDto
{
    public string Address { get; set; }
}