using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.PointsPool;

public class PointsPoolClaimRecordIndex : AbstractEntity<string>, IIndexBuild
{
    public long Amount { get; set; }
    [Keyword] public string PoolId { get; set; }
    [Keyword] public string Address { get; set; }
    [Keyword] public string Seed { get; set; }
    [Keyword] public string Signature { get; set; }
    public ClaimStatus ClaimStatus { get; set; }
    public long CreateTime { get; set; }
    public long ExpiredTime { get; set; }
}

public enum ClaimStatus
{
    Claiming = 0,
    Claimed = 1
}