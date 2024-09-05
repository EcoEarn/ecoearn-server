using System.Collections.Generic;

namespace EcoEarnServer.Background.Services.Dtos;

public class StakingPointsDto
{
    public string Address { get; set; }
    public decimal Points { get; set; }
    public string DappId { get; set; }
}

public class AddressStakingPointsDto
{
    public string Address { get; set; }
    public decimal Points { get; set; }
    public List<StakingPointsDto> DappPoints { get; set; }
}