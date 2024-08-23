namespace EcoEarnServer.PointsStaking.Dtos;

public class PointsClaimInput
{
    public string RawTransaction { get; set; }
    public string ChainId { get; set; }
}