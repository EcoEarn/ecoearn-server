using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.TransactionRecord;

public class TransactionRecordIndex : AbstractEntity<string>, IIndexBuild
{
    [Keyword] public string Address { get; set; }
    public TransactionType TransactionType { get; set; }
    [Keyword] public string Amount { get; set; }
    public long CreateTime { get; set; }
    public bool IsFirstTransaction { get; set; }
}

public enum TransactionType
{
    PointsClaim,
    TokenStake,
    TokenAddStake,
    TokenStakeExtend,
    TokenStakeRenew,
    TokenStakeUnlock,
    TokenClaim,
    LpStake,
    LpAddStake,
    LpStakeExtend,
    LpStakeRenew,
    LpStakeUnlock,
    LpClaim,
    LpAddLiquidityAndStake,
    LpLiquidityStake,
    LpLiquidityRemove,
    RewardsEarlyStake,
    RewardsWithdraw,
}