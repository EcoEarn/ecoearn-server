using System.Collections.Generic;
using EcoEarnServer.Rewards.Dtos;

namespace EcoEarnServer.TokenStaking.Provider;

public class TokenStakedIndexerDto
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
}

public class TokenStakedQuery
{
    public TokenStakedIndexerDto GetTokenStakedInfo { get; set; }
}

public class TokenStakedListQuery
{
    public TokenStakedListResult GetStakedInfoList { get; set; }
}

public class TokenStakedListResult
{
    public List<TokenStakedIndexerDto> Data { get; set; }
}

public class UnLockedStakeIdsQuery
{
    public List<string> GetUnLockedStakeIdsAsync { get; set; }
}