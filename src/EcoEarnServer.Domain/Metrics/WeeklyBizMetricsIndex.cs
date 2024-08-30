using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;

namespace EcoEarnServer.Metrics;

public class WeeklyBizMetricsIndex: AbstractEntity<string>, IIndexBuild
{
    public double BizNumber { get; set; }
    public long CreateTime { get; set; }
    public WeeklyBizType WeeklyBizType { get; set; }
}


public enum WeeklyBizType
{
    EarningUsdAmount,
    Dau,
    RegisterAvg,
    Tvl,
    TvlGrowth,
}