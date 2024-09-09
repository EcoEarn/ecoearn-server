using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.Ranking;

[EventName("PointsRankingListEto")]
public class PointsRankingListEto
{
    public List<PointsRankingEto> EventDataList { get; set; }
}

public class PointsRankingEto
{
    public string Id { get; set; }
    public string Address { get; set; }
    public decimal Points { get; set; }
    public long UpdateTime { get; set; }
}