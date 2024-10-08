using System.Collections.Generic;

namespace EcoEarnServer.Background.Options;

public class PointsSnapshotOptions
{
    public string CreateSnapshotCorn { get; set; } = "0 0 0 * * ?";
    public string WeeklyMetricsCorn { get; set; } = "0 0 0 * * 6";
    public int CheckSnapshotPeriod { get; set; } = 1;
    public int SettlePointsRewardsPeriod { get; set; } = 20;
    public int GenerateMetricsPeriod { get; set; } = 5;

    public int BatchSnapshotCount { get; set; } = 100;
    public int BatchStakingPointsSettleCount { get; set; } = 20;
    public int TaskDelayMilliseconds { get; set; } = 1000;
    public int BatchQueryCount { get; set; } = 5000;

    public string PointsServerBaseUrl { get; set; }
    public string SchrodingerServerBaseUrl { get; set; }
    public string DappId { get; set; }
    public int SettleRewardsBeforeDays { get; set; } = -1;
    public int UpdatePoolStakeSumWorkerDelayPeriod { get; set; } = 10000;
    public bool SettleRewards { get; set; }

    public Dictionary<string, ElevenSymbolSubAddressDto> ElevenSymbolSubAddressDic { get; set; } 
    public Dictionary<string, List<NoSettleInfoDto>> NoSettleInfoDic { get; set; } 
    public bool SchrodingerUnBoundPointsSwitch { get; set; } 
    public string SchrodingerDappId { get; set; } 
    public string EcoEarnDappId { get; set; } 
    public List<string> NineSymbolContractAddressList { get; set; }
    public string ChainId { get; set; } = "tDVW";
    public string StakingPointsActionName { get; set; } = "StakeEarnPoints";
}

public class ElevenSymbolSubAddressDto
{
    public string Address { get; set; } 
    public string Amount { get; set; } 
}

public class NoSettleInfoDto
{
    public string Address { get; set; } 
    public string Domain { get; set; } 
}