namespace EcoEarnServer.Grains.Grain.PointsStakeRewards;

public class PointsStakeRewardsSumDto
{
    public string Id { get; set; }
    public string PoolId { get; set; }
    public string PoolName { get; set; }
    public string DappId { get; set; }
    public string Address { get; set; }
    public string Rewards { get; set; }
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
    public string ClaimedAmount { get; set; }
    public string FrozenAmount { get; set; }
    public string TotalRewards { get; set; }
    public string LastSettleAmount { get; set; }
}