using System.Collections.Generic;
using EcoEarnServer.Rewards.Dtos;

namespace EcoEarnServer.Rewards.Provider;

public class RewardsInfoListIndexerDto
{
    public List<RewardsInfoIndexerDto> Data { get; set; }
    public long TotalCount { get; set; }
}

public class RewardsInfoIndexerDto
{
    public string ClaimId { get; set; }
    public string StakeId { get; set; }
    public string Seed { get; set; }
    public string PoolId { get; set; }
    public string DappId { get; set; }
    public string ClaimedAmount { get; set; }
    public string ClaimedSymbol { get; set; }
    public long ClaimedTime { get; set; }
    public string Account { get; set; }
    public PoolTypeEnums PoolType { get; set; }
}

public class RewardsInfoListQuery
{
    public RewardsInfoListIndexerDto GetRewardsInfoList { get; set; }
}