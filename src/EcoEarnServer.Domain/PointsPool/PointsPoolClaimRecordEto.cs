using Volo.Abp.EventBus;

namespace EcoEarnServer.PointsPool;

[EventName("PointsPoolClaimRecordEto")]
public class PointsPoolClaimRecordEto
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