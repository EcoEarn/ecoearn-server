using System.Collections.Generic;

namespace EcoEarnServer.Options;

public class LpPoolRateOptions
{
    public Dictionary<string, double> LpPoolRateDic { get; set; }
    public LpPriceServer LpPriceServer { get; set; }
    public Dictionary<string, string> SymbolMappingsDic { get; set; }
}

public class LpPriceServer
{
    public string ChainId { get; set; }
    public string LpPriceServerBaseUrl { get; set; }
}