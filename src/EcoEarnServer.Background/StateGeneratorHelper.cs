using System;

namespace EcoEarnServer.Background;

public static class StateGeneratorHelper
{
    private const string SnapshotStateRedisKeyPrefix = "EcoEarnServer:SnapshotState:";
    private const string SettleStateRedisKeyPrefix = "EcoEarnServer:SettleState:";

    public static string GenerateSnapshotKey() => SnapshotStateRedisKeyPrefix + DateTime.UtcNow.ToString("yyyyMMdd");

    public static string GenerateSettleKey(int settleRewardsBeforeDays) =>
        SettleStateRedisKeyPrefix + DateTime.UtcNow.AddDays(settleRewardsBeforeDays).ToString("yyyyMMdd");
}