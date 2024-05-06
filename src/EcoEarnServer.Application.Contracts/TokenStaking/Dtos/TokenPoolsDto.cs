namespace EcoEarnServer.TokenStaking.Dtos;

public class TokenPoolsDto
{
    public string PoolName { get; set; }
    public string PoolId { get; set; }
    public string StakeId { get; set; }
    public string ProjectOwner { get; set; }
    public decimal AprMin { get; set; }
    public decimal AprMax { get; set; }
    public string EarnedSymbol { get; set; }
    public string TotalStake { get; set; }
    public string TotalStakeInUsd { get; set; }
    public string StakeSymbol { get; set; }
    public string Earned { get; set; }
    public string EarnedInUsd { get; set; }
    public string Staked { get; set; }
    public string StakedInUsd { get; set; }
    public long UnlockTime { get; set; }
    public decimal StakeApr { get; set; }
}