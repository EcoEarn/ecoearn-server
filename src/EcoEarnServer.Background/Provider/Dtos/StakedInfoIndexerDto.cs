using System.Collections.Generic;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.TokenStaking.Provider;

namespace EcoEarnServer.Background.Provider.Dtos;

public class StakedInfoIndexerDto
{
    public string StakeId { get; set; }
    public string PoolId { get; set; }
    public string StakingToken { get; set; }
    public long StakedAmount { get; set; }
    public long EarlyStakedAmount { get; set; }
    public long ClaimedAmount { get; set; }
    public long StakedBlockNumber { get; set; }
    public long StakedTime { get; set; }
    public long Period { get; set; }
    public string Account { get; set; }
    public long BoostedAmount { get; set; }
    public long RewardDebt { get; set; }
    public long WithdrawTime { get; set; }
    public long RewardAmount { get; set; }
    public long LockedRewardAmount { get; set; }
    public long LastOperationTime { get; set; }
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
    public PoolTypeEnums PoolType { get; set; }
    public LockState LockState { get; set; }
    public long StakingPeriod { get; set; }
}

public class StakedListQuery
{
    public StakedListResult GetStakedInfoList { get; set; }
}

public class StakedListResult
{
    public List<StakedInfoIndexerDto> Data { get; set; }
}