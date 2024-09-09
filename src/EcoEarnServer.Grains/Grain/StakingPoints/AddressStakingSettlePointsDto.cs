using EcoEarnServer.StakingSettlePoints;

namespace EcoEarnServer.Grains.Grain.StakingPoints;

public class AddressStakingSettlePointsDto
{
    public string Id { get; set; }
    public string Address { get; set; }
    public string Points { get; set; }
    public List<StakingSettlePointsDto> DappPoints { get; set; }
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
}