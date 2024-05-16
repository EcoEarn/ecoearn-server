using EcoEarnServer.PointsPool;

namespace EcoEarnServer.Grains.Grain.PointsPool;

public class PointsPoolClaimRecordDto
{
    public string Id { get; set; }
    public long Amount { get; set; }
    public string PoolId { get; set; }
    public string Address { get; set; }
    public string Seed { get; set; }
    public string Signature { get; set; }
    public ClaimStatus ClaimStatus { get; set; }
    public long CreateTime { get; set; }
    public long ExpiredTime { get; set; }
}
