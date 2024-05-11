namespace EcoEarnServer.Grains.Grain.PointsPool;

public class PointsPoolStakeSumDto
{
    public string Id { get; set; }
    public string StakeAmount { get; set; }
    public string PoolId { get; set; }
    public string PoolName { get; set; }
    public string DappId { get; set; }
    public long DailyReward { get; set; }
}