using EcoEarnServer.Rewards.Dtos;
using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.TokenStaking.Dtos;

public class GetTokenPoolsInput : PagedAndSortedResultRequestDto
{
    public string Name { get; set; }
    public string Address { get; set; }
    public string ChainId { get; set; }
    public PoolTypeEnums PoolType { get; set; }
}