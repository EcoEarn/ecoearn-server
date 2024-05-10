using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.PointsStakeRewards;

[EventName("PointsStakeRewardsSumEto")]
public class PointsStakeRewardsSumListEto
{
    public List<PointsStakeRewardsSumEto> EventDataList { get; set; }
}

public class PointsStakeRewardsSumEto
{
    public string Address { get; set; }
    public string PoolId { get; set; }
    public string PoolName { get; set; }
    public string DappId { get; set; }
    public string Rewards { get; set; }
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
}