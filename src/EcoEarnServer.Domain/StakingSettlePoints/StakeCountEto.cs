using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.StakingSettlePoints;

[EventName("StakeCountListEto")]
public class StakeCountListEto
{
    public List<StakeCountEto> EventDataList { get; set; }
}

public class StakeCountEto
{
    public string PoolId { get; set; }
    public long Count { get; set; }
}