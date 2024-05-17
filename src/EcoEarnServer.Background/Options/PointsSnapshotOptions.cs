namespace EcoEarnServer.Background.Options;

public class PointsSnapshotOptions
{
    public string CreateSnapshotCorn { get; set; } = "0 0 0 * * ?";
    public int CheckSnapshotPeriod { get; set; } = 1;
    public int SettlePointsRewardsPeriod { get; set; } = 20;
    public int UpdatePoolStakeSumPeriod { get; set; } = 20;

    public int BatchSnapshotCount { get; set; } = 100;
    public int BatchQueryCount { get; set; } = 5000;

    public string PointsServerBaseUrl { get; set; }
    public string DappId { get; set; }
    public int SettleRewardsBeforeDays { get; set; } = -1;
    public int UpdatePoolStakeSumWorkerDelayPeriod { get; set; } = 10000;
}