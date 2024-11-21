namespace EcoEarnServer.Grains.Grain.PointsSnapshot;

[GenerateSerializer]
public class PointsSnapshotDto
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public string Domain { get; set; }
    [Id(2)]
    public string Address { get; set; }
    [Id(3)]
    public string DappId { get; set; }
    [Id(4)]
    public string FirstSymbolAmount { get; set; } = "0";
    [Id(5)]
    public string SecondSymbolAmount { get; set; } = "0";
    [Id(6)]
    public string ThirdSymbolAmount { get; set; } = "0";
    [Id(7)]
    public string FourSymbolAmount { get; set; } = "0";
    [Id(8)]
    public string FiveSymbolAmount { get; set; } = "0";
    [Id(9)]
    public string SixSymbolAmount { get; set; } = "0";
    [Id(10)]
    public string SevenSymbolAmount { get; set; } = "0";
    [Id(11)]
    public string EightSymbolAmount { get; set; } = "0";
    [Id(12)]
    public string NineSymbolAmount { get; set; } = "0";
    [Id(13)]
    public string TenSymbolAmount { get; set; }  = "0";
    [Id(14)]
    public string ElevenSymbolAmount { get; set; }  = "0";
    [Id(15)]
    public string TwelveSymbolAmount { get; set; }  = "0";
    [Id(16)]
    public long UpdateTime { get; set; }
    [Id(17)]
    public long CreateTime { get; set; }
    [Id(18)]
    public string SnapshotDate { get; set; }
}