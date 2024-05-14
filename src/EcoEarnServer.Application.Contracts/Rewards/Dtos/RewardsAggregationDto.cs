using System.Collections.Generic;

namespace EcoEarnServer.Rewards.Dtos;

public class RewardsAggregationDto
{
    public PointsPoolAggDto PointsPoolAgg { get; set; }
    public TokenPoolAggDto TokenPoolAgg { get; set; }
    public TokenPoolAggDto LpPoolAgg { get; set; }
}

public class PointsPoolAggDto
{
    public string Total { get; set; }
    public string TotalInUsd { get; set; } = "0";
    public string RewardsTotal { get; set; }
    public string RewardsTotalInUsd { get; set; } = "0";
    public string RewardsTokenName { get; set; }
    public int Decimal { get; set; } = 8;
    public List<string> StakeClaimIds { get; set; }
    public List<string> WithDrawClaimIds { get; set; }
}

public class TokenPoolAggDto
{
    public string RewardsTotal { get; set; }
    public string RewardsTotalInUsd { get; set; } = "0";
    public int Decimal { get; set; } = 8;
    public string RewardsTokenName { get; set; }
    public List<string> WithDrawClaimIds { get; set; }
}