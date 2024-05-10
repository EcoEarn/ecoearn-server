namespace EcoEarnServer.TokenStaking.Provider;

public class RewardDataDto
{
    public string StakeId { get; set; }
    public string Account { get; set; }
    public string Symbol { get; set; }
    public string Amount { get; set; } = "0";
}