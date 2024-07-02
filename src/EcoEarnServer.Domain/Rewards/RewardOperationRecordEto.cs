using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.Rewards;

[EventName("RewardOperationRecordEto")]
public class RewardOperationRecordEto
{
    public string Id { get; set; }
    public long Amount { get; set; }
    public string Address { get; set; }
    public string Seed { get; set; }
    public string Signature { get; set; }
    public List<ClaimInfoDto> ClaimInfos { get; set; }
    public List<string> LiquidityIds { get; set; }
    public ExecuteStatus ExecuteStatus { get; set; }
    public ExecuteType ExecuteType { get; set; }
    public long CreateTime { get; set; }
    public long ExpiredTime { get; set; }
}

public class ClaimInfoDto
{
    public string ClaimId { get; set; }
    public long ReleaseTime { get; set; }
}