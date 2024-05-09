using System.Collections.Generic;

namespace EcoEarnServer.TokenStaking.Dtos;

public class TokenPoolsDto
{
    //token pool info
    public string PoolName { get; set; }
    public string PoolId { get; set; }
    public string ProjectOwner { get; set; }
    public string StakeSymbol { get; set; }
    public string EarnedSymbol { get; set; }
    public double AprMin { get; set; }
    public double AprMax { get; set; }
    public string TotalStake { get; set; }
    public string TotalStakeInUsd { get; set; }
    public long YearlyRewards { get; set; }
    public List<string> Icons { get; set; }

    //stake info
    public string StakeId { get; set; }
    public string Earned { get; set; }
    public string EarnedInUsd { get; set; }
    public string Staked { get; set; }
    public string StakedInUsd { get; set; }
    public string StakedAmount { get; set; }
    public string EarlyStakedAmount { get; set; }
    public long UnlockTime { get; set; }
    public double StakeApr { get; set; }
    public long StakedTime { get; set; }
    public long Period { get; set; }
}