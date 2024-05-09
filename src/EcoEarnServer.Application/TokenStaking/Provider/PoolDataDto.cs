namespace EcoEarnServer.TokenStaking.Provider;

public class PoolDataDto
{
    public string PoolId { get; set; }
    public string AccTokenPerShare { get; set; }
    public string LastRewardBlock { get; set; }
    public string TotalStakedAmount { get; set; }
}