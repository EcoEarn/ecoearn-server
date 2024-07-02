using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.PointsStaking.Dtos;

public class GetPointsPoolsInput : PagedAndSortedResultRequestDto
{
    public PoolQueryType Type { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
}

public enum PoolQueryType
{
    All,
    Staked
}