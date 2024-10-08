using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace EcoEarnServer.PointsSnapshot;

[EventName("PointsSnapshotListEto")]
public class PointsSnapshotListEto
{
    public List<PointsSnapshotEto> EventDataList { get; set; }
}

public class PointsSnapshotEto
{
    public string Id { get; set; }
    public string Domain { get; set; }
    public string Address { get; set; }
    public string DappId { get; set; }
    public string FirstSymbolAmount { get; set; } = "0";
    public string SecondSymbolAmount { get; set; } = "0";
    public string ThirdSymbolAmount { get; set; } = "0";
    public string FourSymbolAmount { get; set; } = "0";
    public string FiveSymbolAmount { get; set; } = "0";
    public string SixSymbolAmount { get; set; } = "0";
    public string SevenSymbolAmount { get; set; } = "0";
    public string EightSymbolAmount { get; set; } = "0";
    public string NineSymbolAmount { get; set; } = "0";
    public string TenSymbolAmount { get; set; }  = "0";
    public string ElevenSymbolAmount { get; set; }  = "0";
    public string TwelveSymbolAmount { get; set; }  = "0";
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
    public string SnapshotDate { get; set; }
}