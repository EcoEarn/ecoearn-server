using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.Ranking;

public class PointsRankingIndex : AbstractEntity<string>, IIndexBuild
{
    [Keyword] public string Address { get; set; }
    public decimal Points { get; set; }
    public long UpdateTime { get; set; }
}