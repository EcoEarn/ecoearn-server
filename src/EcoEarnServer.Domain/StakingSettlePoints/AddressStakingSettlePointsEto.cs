using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.StakingSettlePoints;

[EventName("AddressStakingSettlePointsEto")]
public class AddressStakingSettlePointsListEto
{
    public List<AddressStakingSettlePointsEto> EventDataList { get; set; }
}

public class AddressStakingSettlePointsEto
{
    public string Id { get; set; }
    public string Address { get; set; }
    public string Points { get; set; }
    public List<StakingSettlePointsDto> DappPoints { get; set; }
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
}

public class StakingSettlePointsDto
{
    public string Points { get; set; }
    public string DappId { get; set; }
}