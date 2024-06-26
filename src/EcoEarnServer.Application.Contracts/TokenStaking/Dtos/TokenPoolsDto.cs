using System.Collections.Generic;
using EcoEarnServer.Rewards.Dtos;

namespace EcoEarnServer.TokenStaking.Dtos;

public class TokenPoolsDto
{
    //token pool info
    public string PoolName { get; set; }
    public string PoolId { get; set; }
    public long UnlockWindowDuration { get; set; }
    public string ProjectOwner { get; set; }
    public string StakeSymbol { get; set; }
    public string EarnedSymbol { get; set; }
    public double AprMin { get; set; }
    public double AprMax { get; set; }
    public double StakeApr { get; set; }
    public string TotalStake { get; set; } = "0";
    public string TotalStakeInUsd { get; set; } = "0";
    public long YearlyRewards { get; set; }
    public List<string> Icons { get; set; }
    public double Rate { get; set; }
    public long FixedBoostFactor { get; set; }
    public long ReleasePeriod { get; set; }
    public long MinimumClaimAmount { get; set; }
    
    //stake info
    public string StakeId { get; set; }
    public string Earned { get; set; } = "0";
    public string EarnedInUsd { get; set; } = "0";
    public double UsdRate { get; set; }
    public string Staked { get; set; } = "0";
    public string StakedInUsd { get; set; } = "0";
    public long UnlockTime { get; set; }
    public long StakingPeriod { get; set; }
    public long LongestReleaseTime { get; set; }
    public long LastOperationTime { get; set; }
    public int Decimal { get; set; } = 8;
    public long LatestClaimTime { get; set; }
    public long EarlyStakedAmount { get; set; }
    public List<SubStakeInfoDto> StakeInfos { get; set; } = new();
}