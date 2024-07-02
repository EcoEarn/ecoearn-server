using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.PointsPool;

[EventName("PointsPoolStakeSumEto")]
public class PointsPoolStakeSumListEto
{
    public List<PointsPoolStakeSumEto> EventDataList { get; set; }
}

public class PointsPoolStakeSumEto
{
    public string Id { get; set; }
    public string StakeAmount { get; set; }
    public string PoolId { get; set; }
    public string PoolName { get; set; }
    public string DappId { get; set; }
}