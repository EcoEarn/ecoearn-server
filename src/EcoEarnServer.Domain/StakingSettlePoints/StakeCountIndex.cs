using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.StakingSettlePoints;

public class StakeCountIndex : AbstractEntity<string>, IIndexBuild
{
    [Keyword] public string PoolId { get; set; }
    public long Count { get; set; }
}