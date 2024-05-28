using System.Collections.Generic;

namespace EcoEarnServer.TokenStaking.Provider;

public class PriceDataDto
{
    public string BaseCurrency { get; set; }
    public string QuoteCurrency { get; set; }
}

public class LpPriceDto
{
    public List<LpPriceItemDto> Items { get; set; }
}

public class LpPriceItemDto
{
    public double Tvl { get; set; }
    public string TotalSupply { get; set; }
}