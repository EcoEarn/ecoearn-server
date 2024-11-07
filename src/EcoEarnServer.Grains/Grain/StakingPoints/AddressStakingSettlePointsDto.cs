using EcoEarnServer.StakingSettlePoints;

namespace EcoEarnServer.Grains.Grain.StakingPoints;

[GenerateSerializer]
public class AddressStakingSettlePointsDto
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public string Address { get; set; }
    [Id(2)]
    public string Points { get; set; }
    [Id(3)]
    public List<StakingSettlePointsDto> DappPoints { get; set; }
    [Id(4)]
    public long CreateTime { get; set; }
    [Id(5)]
    public long UpdateTime { get; set; }
}