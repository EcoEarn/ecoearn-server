using System.Collections.Generic;

namespace EcoEarnServer.TokenStaking.Provider;

public class MarketCapDto
{
    public Dictionary<string, List<MarketCapInfoDto>> Data { get; set; }
}

public class MarketCapInfoDto
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Symbol { get; set; }
    public QuoteInfoDto Quote { get; set; }
}

public class QuoteInfoDto
{
    public MarketCapUsd USD { get; set; }
}

public class MarketCapUsd
{
    public decimal Market_Cap { get; set; }
}