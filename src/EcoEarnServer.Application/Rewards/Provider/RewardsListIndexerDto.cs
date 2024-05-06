using System.Collections.Generic;

namespace EcoEarnServer.Rewards.Provider;

public class RewardsListIndexerDto
{
}

public class RewardsListQuery
{
    public RewardsListIndexerResult GetRewardsList { get; set; }
}

public class RewardsListIndexerResult
{
    public List<RewardsListIndexerDto> Data { get; set; }
}