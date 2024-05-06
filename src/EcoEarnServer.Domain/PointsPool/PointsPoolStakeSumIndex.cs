using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;

namespace EcoEarnServer.PointsPool;

public class PointsPoolStakeSumIndex : AbstractEntity<string>, IIndexBuild
{
    public string PoolId { get; set; }
    public string PoolName { get; set; }
    public string DappId { get; set; }
    public string StakeAmount { get; set; }
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
}