using System.Collections.Generic;

namespace EcoEarnServer.PointsStaking.Dtos;

public class AddressRewardsDto
{
    public Dictionary<string, string> Reward { get; set; }
}

public class AddressRewardsSumDto
{
    public string TotalReward { get; set; }
}