using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Ranking.Dtos;
using EcoEarnServer.Ranking.Provider;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Ranking;

public class RankingService : IRankingService, ISingletonDependency
{
    private readonly IRankingProvider _rankingProvider;
    private readonly IObjectMapper _objectMapper;

    public RankingService(IRankingProvider rankingProvider, IObjectMapper objectMapper)
    {
        _rankingProvider = rankingProvider;
        _objectMapper = objectMapper;
    }

    public async Task<RankingInfoDto> GetRankingListAsync(GetRankingListInput input)
    {
        var (total, list) = await _rankingProvider.GetPointsRankingListAsync(input.SkipCount, input.MaxResultCount);
        var rankingList = new List<RankingDto>();
        OwnerRankingDto ownerRankingDto = null;
        for (var i = 0; i < list.Count; i++)
        {
            var pointsRankingIndex = list[i];
            var rankingDto = _objectMapper.Map<PointsRankingIndex, RankingDto>(pointsRankingIndex);
            var isOwner = rankingDto.Address == input.Address;
            rankingDto.IsOwner = isOwner;
            if (isOwner)
            {
                ownerRankingDto = new OwnerRankingDto
                {
                    Address = pointsRankingIndex.Address,
                    Points = pointsRankingIndex.Points,
                    Ranking = i > 99 ? 0 : i + 1
                };
            }

            rankingList.Add(rankingDto);
        }

        if (ownerRankingDto == null)
        {
            var ownerRankingPoints = await _rankingProvider.GetOwnerRankingPointsAsync(input.Address);
            if (ownerRankingPoints != null)
            {
                var topList = await _rankingProvider.GetPointsRankingListAsync(0, 100);
                var top = topList.Item2.Select(x => x.Address).ToList();
                var ranking = 0;
                for (var i = 0; i < top.Count; i++)
                {
                    if (input.Address == top[i])
                    {
                        ranking = i + 1;
                    }
                }

                ownerRankingDto = new OwnerRankingDto
                {
                    Address = ownerRankingPoints.Address,
                    Points = ownerRankingPoints.Points,
                    Ranking = ranking
                };
            }
        }

        return new RankingInfoDto
        {
            OwnerPointsInfo = ownerRankingDto,
            RankingInfo = new RankingListDto
            {
                TotalRecord = total,
                List = rankingList
            }
        };
    }

    public Task<bool> JoinCheckAsync(string chainId, string address)
    {
        return _rankingProvider.JoinCheckAsync(chainId, address);
    }
}