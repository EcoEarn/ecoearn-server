using System.Collections.Generic;
using Orleans;
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

[GenerateSerializer]
public class ClaimInfoDto
{
    [Id(0)]
    public string ClaimId { get; set; }
    [Id(1)]
    public long ReleaseTime { get; set; }
}