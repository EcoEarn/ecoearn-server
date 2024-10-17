namespace EcoEarnServer.Grains.Grain.PointsPool;

[GenerateSerializer]
public class PointsPoolAddressStakeDto
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public string PoolId { get; set; }
    [Id(2)]
    public string PoolName { get; set; }
    [Id(3)]
    public string DappId { get; set; }
    [Id(4)]
    public string Address { get; set; }
    [Id(5)]
    public string StakeAmount { get; set; }
    [Id(6)]
    public long CreateTime { get; set; }
    [Id(7)]
    public long UpdateTime { get; set; }
}