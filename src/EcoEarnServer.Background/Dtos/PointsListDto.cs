using System;
using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Background.Dtos;

public class PointsListDto
{
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
    public OperatorRole Role { get; set; }
    public string Id { get; set; }
    public long BlockHeight { get; set; }
}

public enum OperatorRole
{
    Inviter,
    Kol,
    User,
    All = -1
}

public class GetPointsListInput : PagedAndSortedResultRequestDto
{
    public string DappName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}