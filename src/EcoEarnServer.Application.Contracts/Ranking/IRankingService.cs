using System.Threading.Tasks;
using EcoEarnServer.Ranking.Dtos;

namespace EcoEarnServer.Ranking;

public interface IRankingService
{
    Task<RankingInfoDto> GetRankingListAsync(GetRankingListInput input);
    Task<bool> JoinCheckAsync(string chainId, string address);
}