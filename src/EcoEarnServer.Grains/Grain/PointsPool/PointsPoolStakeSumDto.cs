namespace EcoEarnServer.Grains.Grain.PointsPool;

[GenerateSerializer]
public class PointsPoolStakeSumDto
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public string StakeAmount { get; set; }
    [Id(2)]
    public string PoolId { get; set; }
    [Id(3)]
    public string PoolName { get; set; }
    [Id(4)]
    public string DappId { get; set; }
    [Id(5)]
    public decimal DailyReward { get; set; }
    [Id(6)]
    public string PointsName { get; set; }
}