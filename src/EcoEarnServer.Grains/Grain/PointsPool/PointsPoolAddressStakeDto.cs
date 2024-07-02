namespace EcoEarnServer.Grains.Grain.PointsPool;

public class PointsPoolAddressStakeDto
{
    public string Id { get; set; }
    public string PoolId { get; set; }
    public string PoolName { get; set; }
    public string DappId { get; set; }
    public string Address { get; set; }
    public string StakeAmount { get; set; }
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
}