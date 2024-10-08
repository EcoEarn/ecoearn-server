using System.Collections.Generic;

namespace EcoEarnServer.Rewards.Dtos;

public class LiquiditySignatureInput
{
    public List<string> LiquidityIds { get; set; }
    public string PoolId { get; set; }
    public long LpAmount { get; set; }
    public long Period { get; set; }
    public string DappId { get; set; }
    public long TokenAMin { get; set; }
    public long TokenBMin { get; set; }
    public string Address { get; set; }
}