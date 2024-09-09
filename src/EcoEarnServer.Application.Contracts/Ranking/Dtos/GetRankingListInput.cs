using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Ranking.Dtos;

public class GetRankingListInput : PagedResultRequestDto
{
    public string Address { get; set; }
}