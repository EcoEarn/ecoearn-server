using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.Metrics;

public class BizMetricsIndex : AbstractEntity<string>, IIndexBuild
{
    [Keyword] public string BizNumber { get; set; }
    public long CreateTime { get; set; }
    public BizType BizType { get; set; }
}

public enum BizType
{
    PlatformStakedUsdAmount,
    RegisterCount,
    PlatformEarning,
    TokenStakedAddressCount,
    TokenStakedAmount,
    TokenStakedUsdAmount,
    LpStakedAddressCount,
    LpStakedAmount,
    LpStakedUsdAmount,
}