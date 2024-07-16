using System.Collections.Generic;

namespace EcoEarnServer.Farm.Dtos;

public class LiquidityInfoDto
{
    public List<string> LiquidityIds { get; set; } = new();
    public string LpSymbol { get; set; }
    public List<string> Icons { get; set; } = new() { "", "" };
    public double Rate { get; set; }
    public string Banlance { get; set; } = "0";
    public string StakingAmount { get; set; } = "0";
    public string Value { get; set; } = "0";
    public string TokenAAmount { get; set; } = "0";
    public string TokenAUnStakingAmount { get; set; } = "0";
    public string TokenASymbol { get; set; }
    public string TokenBAmount { get; set; } = "0";
    public string TokenBUnStakingAmount { get; set; } = "0";
    public string TokenBSymbol { get; set; }
    public int Decimal { get; set; } = 8;
    public int UsdDecimal { get; set; } = 6;
    public string RewardSymbol { get; set; }
    public long LpAmount { get; set; }
}

public enum LpStatus
{
    Added,
    Removed
}

public class MarketLiquidityInfoDto
{
    public List<string> LiquidityIds { get; set; } = new();
    public string LpSymbol { get; set; }
    public List<string> Icons { get; set; } = new() { "", "" };
    public double Rate { get; set; }
    public string Banlance { get; set; }
    public string StakingAmount { get; set; } = "0";
    public string Value { get; set; }
    public string TokenAAmount { get; set; } = "0";
    public string EcoEarnTokenAUnStakingAmount { get; set; } = "0";
    public string TokenASymbol { get; set; }
    public string TokenBAmount { get; set; } = "0";
    public string EcoEarnTokenBUnStakingAmount { get; set; } = "0";
    public string TokenBSymbol { get; set; }
    public int Decimal { get; set; } = 8;
    public int UsdDecimal { get; set; } = 6;
    public string RewardSymbol { get; set; }
    public string EcoEarnTokenAAmount { get; set; } = "0";
    public string EcoEarnTokenBAmount { get; set; } = "0";
    public string EcoEarnBanlance { get; set; } = "0";
    public long LpAmount { get; set; }
}