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
    public double Price { get; set; }
    public double PriceUSD { get; set; }
    public double PricePercentChange24h { get; set; }
    public double PriceChange24h { get; set; }
    public double PriceHigh24h { get; set; }
    public double PriceHigh24hUSD { get; set; }
    public double PriceLow24h { get; set; }
    public double PriceLow24hUSD { get; set; }
    public double Volume24h { get; set; }
    public double VolumePercentChange24h { get; set; }
    public double TradeValue24h { get; set; }
    public double Tvl { get; set; }
    public double TvlPercentChange24h { get; set; }
    public double ValueLocked0 { get; set; }
    public double ValueLocked1 { get; set; }
    public int TradeCount24h { get; set; }
    public int TradeAddressCount24h { get; set; }
    public double FeePercent7d { get; set; }
    public string TotalSupply { get; set; }
    public bool IsFav { get; set; }
    public object FavId { get; set; }
    public string ChainId { get; set; }
    public string Address { get; set; }
    public double FeeRate { get; set; }
    public bool IsTokenReversed { get; set; }
    public LpTokenDto Token0 { get; set; }
    public LpTokenDto Token1 { get; set; }
    public string Id { get; set; }
}

public class LpTokenDto
{
    public string Address { get; set; }
    public string Symbol { get; set; }
    public int Decimals { get; set; }
    public string ChainId { get; set; }
    public string Id { get; set; }
}