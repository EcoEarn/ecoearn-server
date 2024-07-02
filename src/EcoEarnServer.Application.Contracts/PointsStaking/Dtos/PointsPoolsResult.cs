using System.Collections.Generic;
using EcoEarnServer.TokenStaking.Dtos;

namespace EcoEarnServer.PointsStaking.Dtos;

public class PointsPoolsResult
{
    public List<PointsPoolsDto> Pools { get; set; }
    public List<TextNodeDto> TextNodes { get; set; }
}