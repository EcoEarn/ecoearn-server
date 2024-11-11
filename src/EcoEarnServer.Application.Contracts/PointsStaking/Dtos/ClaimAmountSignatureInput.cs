namespace EcoEarnServer.PointsStaking.Dtos;

public class ClaimAmountSignatureInput
{
    public long Amount { get; set; }
    public string PoolId { get; set; }
    public string Address { get; set; }
    public string Domain { get; set; }
}