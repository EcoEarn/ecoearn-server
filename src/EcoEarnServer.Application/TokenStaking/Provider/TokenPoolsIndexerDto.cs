using System.Collections.Generic;
using EcoEarnServer.Rewards.Dtos;

namespace EcoEarnServer.TokenStaking.Provider;

public class TokenPoolsIndexerDto
{
    public string DappId { get; set; }
    public string PoolId { get; set; }
    public string PoolAddress { get; set; }
    public string Amount { get; set; }
    public TokenPoolConfigIndexerDto TokenPoolConfig { get; set; }
    public long CreateTime { get; set; }
    public PoolTypeEnums PoolType { get; set; }
}

public class TokenPoolConfigIndexerDto
{
    public string RewardToken { get; set; }
    public long StartBlockNumber { get; set; }
    public long EndBlockNumber { get; set; }
    public long RewardPerBlock { get; set; }
    public string StakingToken { get; set; }
    public long FixedBoostFactor { get; set; }
    public long MinimumAmount { get; set; }
    public long ReleasePeriod { get; set; }
    public long MaximumStakeDuration { get; set; }
    public string RewardTokenContract { get; set; }
    public string StakeTokenContract { get; set; }
    public long MinimumClaimAmount { get; set; }
    public long MergeInterval { get; set; }
    public long MinimumAddLiquidityAmount { get; set; }
    public long UnlockWindowDuration { get; set; }
}

public class TokenPoolsQuery
{
    public TokenPoolsIndexerResult GetTokenPoolList { get; set; }
}

public class TokenPoolsIndexerResult
{
    public List<TokenPoolsIndexerDto> Data { get; set; }
}