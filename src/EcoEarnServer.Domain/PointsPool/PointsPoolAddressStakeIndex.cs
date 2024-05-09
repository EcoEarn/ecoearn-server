using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.PointsPool;

public class PointsPoolAddressStakeIndex : AbstractEntity<string>, IIndexBuild
{
    [Keyword] public string Address { get; set; }
    [Keyword] public string PoolId { get; set; }
    [Keyword] public string PoolName { get; set; }
    [Keyword] public string DappId { get; set; }
    [Keyword] public string StakeAmount { get; set; }
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
}