using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Rewards.Dtos;

public class GetRewardsListInput : PagedResultRequestDto
{
    public PoolTypeEnums PoolType { get; set; }
    public bool FilterUnlocked { get; set; }
    public string Address { get; set; }
    public string Id { get; set; }
}