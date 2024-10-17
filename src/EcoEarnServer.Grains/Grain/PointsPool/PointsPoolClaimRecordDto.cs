using EcoEarnServer.PointsPool;

namespace EcoEarnServer.Grains.Grain.PointsPool;

[GenerateSerializer]
public class PointsPoolClaimRecordDto
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public long Amount { get; set; }
    [Id(2)]
    public string PoolId { get; set; }
    [Id(3)]
    public string Address { get; set; }
    [Id(4)]
    public string Seed { get; set; }
    [Id(5)]
    public string Signature { get; set; }
    [Id(6)]
    public ClaimStatus ClaimStatus { get; set; }
    [Id(7)]
    public long CreateTime { get; set; }
    [Id(8)]
    public long ExpiredTime { get; set; }
}
