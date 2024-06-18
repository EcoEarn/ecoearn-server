using System.Collections.Generic;
using EcoEarnServer.Rewards.Dtos;

namespace EcoEarnServer.PointsStaking.Dtos;

public class EarlyStakeInfoDto
{
    public string Staked { get; set; }
    public string StakeId { get; set; } = "";
    public string PoolId { get; set; }
    public string StakeSymbol { get; set; } = "";
    public long UnlockTime { get; set; }
    public double StakeApr { get; set; }
    public long YearlyRewards { get; set; }
    public long FixedBoostFactor { get; set; }
    public long StakingPeriod { get; set; }
    public long LongestReleaseTime { get; set; }
    public long UnlockWindowDuration { get; set; }
    public long LastOperationTime { get; set; }
    public long MinimumClaimAmount { get; set; }
    
    public List<SubStakeInfoDto> SubStakeInfos { get; set; }
}