using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.Rewards;

public class RewardOperationRecordIndex : AbstractEntity<string>, IIndexBuild
{
    public long Amount { get; set; }
    [Keyword] public string Address { get; set; }
    [Keyword] public string Seed { get; set; }
    [Keyword] public string Signature { get; set; }
    [Nested(Name = "ClaimInfos", Enabled = true, IncludeInParent = true, IncludeInRoot = true)]
    public List<ClaimInfo> ClaimInfos { get; set; }
    public ExecuteStatus ExecuteStatus { get; set; }
    public ExecuteType ExecuteType { get; set; }
    public long CreateTime { get; set; }
    public long ExpiredTime { get; set; }
}

public class ClaimInfo
{
    [Keyword] public string ClaimId { get; set; }
}

public enum ExecuteType
{
    Withdrawn = 0,
    EarlyStake = 1,
    LiquidityAdded = 2,
}
public enum ExecuteStatus
{
    Executing = 0,
    Ended = 1,
    Cancel = 2,
}