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
    [Keyword] public string SettleDate { get; set; }
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
}