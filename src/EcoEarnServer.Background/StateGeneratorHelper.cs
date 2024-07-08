using System;

namespace EcoEarnServer.Background;

public static class StateGeneratorHelper
{
    private const string SnapshotStateRedisKeyPrefix = "EcoEarnServer:SnapshotState:";
    private const string SettleStateRedisKeyPrefix = "EcoEarnServer:SettleState:";
    private const string MetricsStateRedisKeyPrefix = "EcoEarnServer:MetricsState:";

    public static string GenerateSnapshotKey() => SnapshotStateRedisKeyPrefix + DateTime.UtcNow.ToString("yyyyMMdd");

    public static string GenerateSettleKey(int settleRewardsBeforeDays) =>
        SettleStateRedisKeyPrefix + DateTime.UtcNow.AddDays(settleRewardsBeforeDays).ToString("yyyyMMdd");
    
    public static string GenerateMetricsKey() => MetricsStateRedisKeyPrefix + DateTime.UtcNow.ToString("yyyyMMdd");

}