using System.Collections.Generic;

namespace EcoEarnServer.Farm.Dtos;

public class LiquidityInfoDto
{
    public List<string> LiquidityIds { get; set; } = new();
    public string LpSymbol { get; set; }
    public List<string> Icons { get; set; } = new() { "", "" };
    public double Rate { get; set; }
    public string Banlance { get; set; }
    public string StakingAmount { get; set; }
    public string Value { get; set; }
    public string TokenAAmount { get; set; }
    public string TokenAUnStakingAmount { get; set; }
    public string TokenASymbol { get; set; }
    public string TokenBAmount { get; set; }
    public string TokenBUnStakingAmount { get; set; }
    public string TokenBSymbol { get; set; }
    public int Decimal { get; set; } = 8;
    public int UsdDecimal { get; set; } = 6;
    public string RewardSymbol { get; set; }
    public long LpAmount { get; set; }
    public long LongestReleaseTime { get; set; }
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
    public string StakingAmount { get; set; }
    public string Value { get; set; }
    public string TokenAAmount { get; set; }
    public string EcoEarnTokenAUnStakingAmount { get; set; }
    public string TokenASymbol { get; set; }
    public string TokenBAmount { get; set; }
    public string EcoEarnTokenBUnStakingAmount { get; set; }
    public string TokenBSymbol { get; set; }
    public int Decimal { get; set; } = 8;
    public int UsdDecimal { get; set; } = 6;
    public string RewardSymbol { get; set; }
    public string EcoEarnTokenAAmount { get; set; }
    public string EcoEarnTokenBAmount { get; set; }
    public string EcoEarnBanlance { get; set; }
    public long LpAmount { get; set; }
    public long LongestReleaseTime { get; set; }
}

public class LiquidityInfoListDto
{
    public long LpAmount { get; set; }
    public string LpUsdAmount { get; set; }
    public string LpSymbol { get; set; }
    public string Address { get; set; }
    public long TokenAAmount { get; set; }
    public string TokenASymbol { get; set; }
    public long TokenBAmount { get; set; }
    public string TokenBSymbol { get; set; }
    public long AddedTime { get; set; }
    public string SwapAddress { get; set; }
    public string TokenAddress { get; set; }
}