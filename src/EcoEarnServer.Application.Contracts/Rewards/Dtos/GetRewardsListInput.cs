using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Rewards.Dtos;

public class GetRewardsListInput : PagedResultRequestDto
{
    public PoolTypeEnums PoolType { get; set; }
    public bool FilterUnlocked { get; set; }
}