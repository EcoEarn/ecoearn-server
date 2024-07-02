namespace EcoEarnServer.Common;

public class ContractConstants
{
    public const string StakedSumMethodName = "GetPoolData";
    public const string StakedRewardsMethodName = "GetReward";
    public const string ContractName = "EcoEarnTokens";
    public const string SenderName = "QueryTokenPoolStakedSumAccount";
    public const string UpdateStakeInfoSenderName = "UpdateStakeInfo";
}

public static class TransactionState
{
    public const string Mined = "MINED";
    public const string Pending = "PENDING";
    public const string NotExisted = "NOTEXISTED";
}