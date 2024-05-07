namespace EcoEarnServer.PointsStaking.Dtos;

public class EarlyStakeInfoDto
{
    public string Staked { get; set; }
    public string StakeId { get; set; }
    public string StakeSymbol { get; set; }
    public long StakedTime { get; set; }
    public long UnlockTime { get; set; }
    public double StakeApr { get; set; }
    public long Period { get; set; }
    public long YearlyRewards { get; set; }
}