namespace EcoEarnServer.PointsStaking.Dtos;

public class PointsPoolsDto
{
    public string PoolName { get; set; }
    public string PoolId { get; set; }
    public string DailyRewards { get; set; }
    public decimal PoolDailyRewards { get; set; }
    public string TotalStake { get; set; }
    public string Earned { get; set; }
    public string RealEarned { get; set; }
    public string Staked { get; set; }
    public int Decimal { get; set; } = 8;
    public string RewardsTokenName { get; set; }
    public long ReleasePeriod { get; set; }
    public string StakeTokenName { get; set; }
}