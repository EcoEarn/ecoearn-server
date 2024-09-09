using System.Threading.Tasks;
using EcoEarnServer.Ranking;
using EcoEarnServer.Ranking.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace EcoEarnServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("RankingController")]
[Route("api/app/ranking")]
public class RankingController : EcoEarnServerController
{
    private readonly IRankingService _rankingService;

    public RankingController(IRankingService rankingService)
    {
        _rankingService = rankingService;
    }

    [HttpPost("list")]
    public async Task<RankingInfoDto> GetRankingListAsync(GetRankingListInput input)
    {
        return await _rankingService.GetRankingListAsync(input);
    }

    [HttpGet("join/check")]
    public async Task<bool> JoinCheckAsync(string chainId, string address)
    {
        return await _rankingService.JoinCheckAsync(chainId, address);
    }
}