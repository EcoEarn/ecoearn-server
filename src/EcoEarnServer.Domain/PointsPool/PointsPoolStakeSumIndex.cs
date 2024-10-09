using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.PointsPool;

public class PointsPoolStakeSumIndex : AbstractEntity<string>, IIndexBuild
{
    [Keyword] public string PoolId { get; set; }
    [Keyword] public string PoolName { get; set; }
    [Keyword] public string DappId { get; set; }
    [Keyword] public string StakeAmount { get; set; } = "0";
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
}