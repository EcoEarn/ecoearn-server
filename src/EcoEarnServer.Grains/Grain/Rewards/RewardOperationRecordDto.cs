using EcoEarnServer.Rewards;

namespace EcoEarnServer.Grains.Grain.Rewards;

public class RewardOperationRecordDto
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