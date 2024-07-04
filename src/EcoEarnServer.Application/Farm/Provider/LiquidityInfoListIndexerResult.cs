using System.Collections.Generic;
using EcoEarnServer.Farm.Dtos;

namespace EcoEarnServer.Farm.Provider;

public class LiquidityInfoListIndexerQuery
{
    public LiquidityInfoListIndexerResult GetLiquidityInfoList { get; set; }
}

public class LiquidityInfoListIndexerResult
{
    public List<LiquidityInfoIndexerDto> Data { get; set; } = new();
    public long TotalCount { get; set; }
}

public class LiquidityInfoIndexerDto
{
    public string LiquidityId { get; set; }
    public string StakeId { get; set; }
    public string Seed { get; set; }
    public long LpAmount { get; set; }
    public string LpSymbol { get; set; }
    public string RewardSymbol { get; set; }
    public long TokenAAmount { get; set; }
    public string TokenASymbol { get; set; }
    public long TokenBAmount { get; set; }
    public string TokenBSymbol { get; set; }
    public long AddedTime { get; set; }
    public string DappId { get; set; }
    public string SwapAddress { get; set; }
    public string TokenAddress { get; set; }
    public string TokenALossAmount { get; set; }
    public string TokenBLossAmount { get; set; }
    public LpStatus LpStatus { get; set; }
    public string Address { get; set; }
}