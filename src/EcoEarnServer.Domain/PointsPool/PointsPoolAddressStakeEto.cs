using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.PointsPool;

[EventName("PointsPoolAddressStakeEto")]
public class PointsPoolAddressStakeListEto
{
    public List<PointsPoolAddressStakeEto> EventDataList { get; set; }
}

public class PointsPoolAddressStakeEto
{
    public string Address { get; set; }
    public string PoolId { get; set; }
    public string PoolName { get; set; }
    public string DappId { get; set; }
    public string StakeAmount { get; set; }
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
}