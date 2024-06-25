using System.Collections.Generic;

namespace EcoEarnServer.Farm.Dtos;

public class LiquidityInfoDto
{
    public List<string> LiquidityIds { get; set; } = new();
    public string LpSymbol { get; set; }
    public List<string> Icons { get; set; } = new() { "", "" };
    public double Rate { get; set; }
    public string Banlance { get; set; }
    public string Value { get; set; }
    public string TokenAAmount { get; set; }
    public string TokenASymbol { get; set; }
    public string TokenBAmount { get; set; }
    public string TokenBSymbol { get; set; }
    public int Decimal { get; set; } = 8;
    public int UsdDecimal { get; set; } = 6;
    public string RewardSymbol { get; set; }
}

public enum LpStatus
{
    Added,
    Removed
}