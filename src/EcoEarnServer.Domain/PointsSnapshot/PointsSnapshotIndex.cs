using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;

namespace EcoEarnServer.PointsSnapshot;

public class PointsSnapshotIndex : AbstractEntity<string>, IIndexBuild
{
    public string Domain { get; set; }
    public string Address { get; set; }
    public string FirstSymbolAmount { get; set; }
    public string SecondSymbolAmount { get; set; }
    public string ThirdSymbolAmount { get; set; }
    public string FourSymbolAmount { get; set; }
    public string FiveSymbolAmount { get; set; }
    public string SixSymbolAmount { get; set; }
    public string SevenSymbolAmount { get; set; }
    public string EightSymbolAmount { get; set; }
    public string NineSymbolAmount { get; set; }
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
    public string SnapshotDate { get; set; }
}