using System.Collections.Generic;

namespace EcoEarnServer.Rewards.Dtos;

public class RewardsSignatureInput
{
    public long Amount { get; set; }
    public string Address { get; set; }
    public string DappId { get; set; }
    public PoolTypeEnums PoolType { get; set; }
    public List<string> LiquidityIds { get; set; } = new();

    public List<ClaimInfoDto> ClaimInfos { get; set; } = new();
    
    public string PoolId { get; set; }
    public long Period { get; set; }
    public long TokenAMin { get; set; }
    public long TokenBMin { get; set; }
    public List<string> OperationPoolIds { get; set; }
    public List<string> OperationDappIds { get; set; }
}

public class RewardsEarlyStakeSignatureInput : RewardsSignatureInput
{
    public string PoolId { get; set; }
}

public class RewardsTransactionInput
{
    public string RawTransaction { get; set; }
    public string ChainId { get; set; }
}