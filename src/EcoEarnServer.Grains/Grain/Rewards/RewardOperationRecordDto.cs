using EcoEarnServer.Rewards;

namespace EcoEarnServer.Grains.Grain.Rewards;

[GenerateSerializer]
public class RewardOperationRecordDto
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public long Amount { get; set; }
    [Id(2)]
    public string Address { get; set; }
    [Id(3)]
    public string Seed { get; set; }
    [Id(4)]
    public string Signature { get; set; }
    [Id(5)]
    public List<ClaimInfoDto> ClaimInfos { get; set; }
    [Id(6)]
    public List<string> LiquidityIds { get; set; }
    [Id(7)]
    public ExecuteStatus ExecuteStatus { get; set; }
    [Id(8)]
    public ExecuteType ExecuteType { get; set; }
    [Id(9)]
    public long CreateTime { get; set; }
    [Id(10)]
    public long ExpiredTime { get; set; }
}