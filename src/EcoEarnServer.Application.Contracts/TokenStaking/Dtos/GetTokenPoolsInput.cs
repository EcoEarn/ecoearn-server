using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.TokenStaking.Dtos;

public class GetTokenPoolsInput : PagedAndSortedResultRequestDto
{
    public string Name { get; set; }
}