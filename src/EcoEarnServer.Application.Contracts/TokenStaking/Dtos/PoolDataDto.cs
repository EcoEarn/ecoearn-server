using AElf.Types;

namespace EcoEarnServer.TokenStaking.Dtos;

public class PoolDataDto
{
    public string PoolId { get; set; }
    public BigIntValue AccTokenPerShare { get; set; }
    public string LastRewardTime { get; set; }
    public string TotalStakedAmount { get; set; } = "0";
}