using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.StakingSettlePoints;

public class AddressStakingSettlePointsIndex : AbstractEntity<string>, IIndexBuild
{
    [Keyword] public string Address { get; set; }
    [Keyword] public string Points { get; set; }

    [Nested(Name = "DappPoints", Enabled = true, IncludeInParent = true, IncludeInRoot = true)]
    public List<StakingSettlePoints> DappPoints { get; set; }

    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
}

public class StakingSettlePoints
{
    [Keyword] public string Points { get; set; }
    [Keyword] public string DappId { get; set; }
}