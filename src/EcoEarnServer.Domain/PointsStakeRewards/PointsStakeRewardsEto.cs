using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.PointsStakeRewards;

[EventName("PointsStakeRewardsEto")]
public class PointsStakeRewardsListEto
{
    public List<PointsStakeRewardsEto> EventDataList { get; set; }
}

public class PointsStakeRewardsEto
{
    public string Address { get; set; }
    public string PoolId { get; set; }
    public string PoolName { get; set; }
    public string DappId { get; set; }
    public string Rewards { get; set; }
    public string SettleDate { get; set; }
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
}