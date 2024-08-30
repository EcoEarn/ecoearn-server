using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.Metrics;

[EventName("WeeklyBizMetricsEto")]
public class WeeklyBizMetricsListEto
{
    public List<WeeklyBizMetricsEto> EventDataList { get; set; }
}

public class WeeklyBizMetricsEto
{
    public string Id { get; set; }
    public double BizNumber { get; set; }
    public long CreateTime { get; set; }
    public WeeklyBizType WeeklyBizType { get; set; }
}