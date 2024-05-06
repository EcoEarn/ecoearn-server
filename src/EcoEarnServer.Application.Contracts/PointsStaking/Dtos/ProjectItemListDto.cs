namespace EcoEarnServer.PointsStaking.Dtos;

public class ProjectItemListDto
{
    public string DappName { get; set; }
    public string ProjectOwner { get; set; }
    public string Icon { get; set; }
    public long Tvl { get; set; }
    public long StakingAddress { get; set; }
    public bool IsOpenStake { get; set; }
}