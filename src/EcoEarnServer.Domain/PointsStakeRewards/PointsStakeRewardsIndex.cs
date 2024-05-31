using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.PointsStakeRewards;

public class PointsStakeRewardsIndex : AbstractEntity<string>, IIndexBuild
{
    [Keyword] public string Address { get; set; }
    [Keyword] public string PoolId { get; set; }
    [Keyword] public string PoolName { get; set; }
    [Keyword] public string DappId { get; set; }
    [Keyword] public string Rewards { get; set; }
    public long ReleasePeriod { get; set; }
    [Keyword] public string SettleDate { get; set; }
    [Keyword] public string StartSettleDate { get; set; }
    [Keyword] public string EndSettleDate { get; set; }
    public long CreateTime { get; set; }
    public PeriodState PeriodState { get; set; }
}

public enum PeriodState
{
    InPeriod,
    End,
}