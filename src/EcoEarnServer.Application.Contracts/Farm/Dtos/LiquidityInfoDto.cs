using System.Collections.Generic;

namespace EcoEarnServer.Farm.Dtos;

public class LiquidityInfoDto
{
    public string LpSymbol { get; set; }
    public List<string> Icons { get; set; }
    public double Rate { get; set; }
    public string Banlance { get; set; }
    public string Value { get; set; }
    public string TokenAAmount { get; set; }
    public string TokenASymbol { get; set; }
    public long TokenBAmount { get; set; }
    public string TokenBSymbol { get; set; }
    public int Decimal { get; set; } = 8;
}

public enum LpStatus
{
    Added,
    Removed
}