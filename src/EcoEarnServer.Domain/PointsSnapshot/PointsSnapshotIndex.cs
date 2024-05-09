using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.PointsSnapshot;

public class PointsSnapshotIndex : AbstractEntity<string>, IIndexBuild
{
    [Keyword] public string Domain { get; set; }
    [Keyword] public string Address { get; set; }
    [Keyword] public string DappId { get; set; }
    [Keyword] public string FirstSymbolAmount { get; set; }
    [Keyword] public string SecondSymbolAmount { get; set; }
    [Keyword] public string ThirdSymbolAmount { get; set; }
    [Keyword] public string FourSymbolAmount { get; set; }
    [Keyword] public string FiveSymbolAmount { get; set; }
    [Keyword] public string SixSymbolAmount { get; set; }
    [Keyword] public string SevenSymbolAmount { get; set; }
    [Keyword] public string EightSymbolAmount { get; set; }
    [Keyword] public string NineSymbolAmount { get; set; }
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
    [Keyword] public string SnapshotDate { get; set; }
}