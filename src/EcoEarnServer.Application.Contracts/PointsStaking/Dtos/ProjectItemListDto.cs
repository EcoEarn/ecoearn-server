namespace EcoEarnServer.PointsStaking.Dtos;

public class ProjectItemListDto
{
    public string DappName { get; set; }
    public string DappId { get; set; }
    public string ProjectOwner { get; set; }
    public string Icon { get; set; }
    public string Tvl { get; set; }
    public long StakingAddress { get; set; }
    public bool IsOpenStake { get; set; }
}