using System.Collections.Generic;

namespace EcoEarnServer.TokenStaking.Provider;

public class RewardDataDto
{
    public string StakeId { get; set; }
    public string Account { get; set; }
    public string Symbol { get; set; }
    public long Amount { get; set; }
    public string PoolId { get; set; }
}

public class RewardDataListDto
{
    public List<RewardDataDto> RewardInfos { get; set; }
}