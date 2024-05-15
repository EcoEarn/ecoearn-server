using System.Collections.Generic;
using EcoEarnServer.Rewards.Dtos;

namespace EcoEarnServer.Rewards.Provider;

public class RewardsListIndexerDto
{
    public string Id { get; set; }
    public string ClaimId { get; set; }
    public string StakeId { get; set; }
    public string PoolId { get; set; }
    public string ClaimedAmount { get; set; }
    public string ClaimedSymbol { get; set; }
    public long ClaimedBlockNumber { get; set; }
    public long ClaimedTime { get; set; }
    public long UnlockTime { get; set; }
    public long WithdrawTime { get; set; }
    public long EarlyStakeTime { get; set; }
    public string Account { get; set; }
    public PoolTypeEnums PoolType { get; set; }
}

public class RewardsListQuery
{
    public RewardsListIndexerResult GetClaimInfoList { get; set; }
}

public class RewardsListIndexerResult
{
    public List<RewardsListIndexerDto> Data { get; set; }
    public long TotalCount { get; set; }
}