using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.Metrics;


[EventName("BizMetricsEto")]
public class BizMetricsListEto
{
    public List<BizMetricsEto> EventDataList { get; set; }
}

public class BizMetricsEto
{
    public string Id { get; set; }
    public double BizNumber { get; set; }
    public long CreateTime { get; set; }
    public BizType BizType { get; set; }
}