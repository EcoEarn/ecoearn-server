namespace EcoEarnServer.Background.Services.Dtos;

public class StakePriceDto
{
    public string Amount { get; set; }
    public string PoolId { get; set; }
    public double UsdAmount { get; set; }
    public string Rate { get; set; }
}