namespace EcoEarnServer.PointsStaking.Dtos;

public class PointsPoolsDto
{
    public string PoolName { get; set; }
    public string PoolId { get; set; }
    public long DailyRewards { get; set; }
    public long PoolDailyRewards { get; set; }
    public string TotalStake { get; set; }
    public string Earned { get; set; }
    public string Staked { get; set; }
    public int Decimal { get; set; } = 8;
}