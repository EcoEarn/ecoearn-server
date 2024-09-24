using System.Collections.Generic;

namespace EcoEarnServer.Rewards.Dtos;

public class RewardsAggregationDto
{
    public string DappId { get; set; }
    public string PoolName { get; set; }
    public string PoolId { get; set; }
    public List<string> TokenIcon { get; set; }
    public string RewardsTokenName { get; set; }
    public double Sort { get; set; }
    public double Rate { get; set; }
    public string PoolType { get; set; }
    public PoolTypeEnums PoolTypeEnums { get; set; }
    public bool SupportEarlyStake { get; set; }
    public RewardsAggDto RewardsInfo { get; set; }
}

public class RewardsAggDto
{
    public string TotalRewards { get; set; }
    public string TotalRewardsInUsd { get; set; } = "0";
    public string RewardsTokenName { get; set; }
    public int Decimal { get; set; } = 8;

    public string Withdrawn { get; set; }
    public string WithdrawnInUsd { get; set; } = "0";

    public string Frozen { get; set; }
    public string FrozenInUsd { get; set; } = "0";

    public string Withdrawable { get; set; }
    public string WithdrawableInUsd { get; set; } = "0";

    public string EarlyStakedAmount { get; set; }
    public string EarlyStakedAmountInUsd { get; set; } = "0";

    public long NextRewardsRelease { get; set; }
    public string NextRewardsReleaseAmount { get; set; } = "0";

    public List<ClaimInfoDto> ClaimInfos { get; set; }
    public List<ClaimInfoDto> WithdrawableClaimInfos { get; set; }
    public bool AllRewardsRelease { get; set; }
    public long FirstClaimTime { get; set; }
}

public class PointsPoolAggDto
{
    public string Total { get; set; }
    public string TotalInUsd { get; set; } = "0";
    public string RewardsTotal { get; set; }
    public string RewardsTotalInUsd { get; set; } = "0";
    public string RewardsTokenName { get; set; }
    public int Decimal { get; set; } = 8;
    public List<string> StakeClaimIds { get; set; }
    public List<string> WithDrawClaimIds { get; set; }
}

public class TokenPoolAggDto
{
    public string RewardsTotal { get; set; }
    public string RewardsTotalInUsd { get; set; } = "0";
    public int Decimal { get; set; } = 8;
    public string RewardsTokenName { get; set; }
    public List<string> WithDrawClaimIds { get; set; }
}