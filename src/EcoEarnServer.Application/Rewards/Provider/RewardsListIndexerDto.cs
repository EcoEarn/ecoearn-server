using System.Collections.Generic;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.TokenStaking.Provider;

namespace EcoEarnServer.Rewards.Provider;

public class RewardsListIndexerDto
{
    public string Id { get; set; }
    public string ClaimId { get; set; }
    public string StakeId { get; set; }
    public string PoolId { get; set; }
    public string ClaimedAmount { get; set; }
    public string EarlyStakedAmount { get; set; }
    public string Seed { get; set; }
    public string ClaimedSymbol { get; set; }
    public long ClaimedBlockNumber { get; set; }
    public long ClaimedTime { get; set; }
    public long ReleaseTime { get; set; }
    public long WithdrawTime { get; set; }
    public long EarlyStakeTime { get; set; }
    public string Account { get; set; }
    public PoolTypeEnums PoolType { get; set; }
    public LockState PoolLockStateType { get; set; }
    public string WithdrawSeed { get; set; }
    public string LiquidityId { get; set; }
    public string ContractAddress { get; set; }
    public string EarlyStakeSeed { get; set; }
    public string LiquidityAddedSeed { get; set; }
}

public class RewardsListQuery
{
    public RewardsListIndexerResult GetClaimInfoList { get; set; }
}

public class RewardsCountQuery
{
    public long GetClaimInfoCount { get; set; }
}

public class RealRewardsListQuery
{
    public RewardsListIndexerResult GetRealClaimInfoList { get; set; }
}


public class RewardsListIndexerResult
{
    public List<RewardsListIndexerDto> Data { get; set; }
    public long TotalCount { get; set; }
}

public class RewardsDto
{
    public string ClaimedAmount { get; set; } = "0";
    public long ReleaseTime { get; set; }
    public string ClaimId { get; set; }
}

public class RewardsMergeDto
{
    public string ClaimedAmount { get; set; } = "0";
    public long ReleaseTime { get; set; }
    public List<string> ClaimIds { get; set; } = new();
    public string Frozen { get; set; } = "0";
}
