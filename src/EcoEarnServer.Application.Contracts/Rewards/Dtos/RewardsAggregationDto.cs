namespace EcoEarnServer.Rewards.Dtos;

public class RewardsAggregationDto
{
    public PointsPoolAggDto PointsPoolAgg { get; set; }
    public TokenPoolAggDto TokenPoolAgg { get; set; }
}

public class PointsPoolAggDto
{
    public long Total { get; set; }
    public long RewardsTotal { get; set; }
}

public class TokenPoolAggDto
{
    public long RewardsTotal { get; set; }
}