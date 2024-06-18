namespace EcoEarnServer.Rewards.Dtos;

public class SubStakeInfoDto
{
    public string SubStakeId { get; set; }
    public long StakedAmount { get; set; }
    public long StakedBlockNumber { get; set; }
    public long StakedTime { get; set; }
    public long Period { get; set; }
    public long BoostedAmount { get; set; }
    public long RewardDebt { get; set; }
    public long RewardAmount { get; set; }
    public double Apr { get; set; }
}