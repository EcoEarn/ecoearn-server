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
    public string TotalStake { get; set; } = "0";
    public string TotalStakeInUsd { get; set; } = "0";
    public long YearlyRewards { get; set; }
    public List<string> Icons { get; set; }

    //stake info
    public string StakeId { get; set; }
    public string Earned { get; set; } = "0";
    public string EarnedInUsd { get; set; } = "0";
    public string Staked { get; set; } = "0";
    public string StakedInUsd { get; set; } = "0";
    public string StakedAmount { get; set; } = "0";
    public string EarlyStakedAmount { get; set; } = "0";
    public long UnlockTime { get; set; }
    public double StakeApr { get; set; }
    public long StakedTime { get; set; }
    public long Period { get; set; }
}