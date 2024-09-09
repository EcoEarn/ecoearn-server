using System.Collections.Generic;

namespace EcoEarnServer.Ranking.Dtos;

public class RankingInfoDto
{
    public OwnerRankingDto OwnerPointsInfo { get; set; }
    public RankingListDto RankingInfo { get; set; }
}

public class OwnerRankingDto
{
    public string Address { get; set; }
    public decimal Points { get; set; }
    public long Ranking { get; set; }
}

public class RankingListDto
{
    public long TotalRecord { get; set; }
    public List<RankingDto> List { get; set; }
}

public class RankingDto
{
    public string Address { get; set; }
    public decimal Points { get; set; }
    public bool IsOwner { get; set; }
}