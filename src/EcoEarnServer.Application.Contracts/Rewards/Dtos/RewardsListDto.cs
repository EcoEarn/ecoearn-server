using System.Collections.Generic;

namespace EcoEarnServer.Rewards.Dtos;

public class RewardsListDto
{
    public PoolTypeEnums PooType { get; set; }
    public string ProjectOwner { get; set; }
    public string RewardsToken { get; set; }
    public string RewardsInUsd { get; set; } = "0";
    public string Rewards { get; set; }
    public string ClaimedId { get; set; }
    public string TokenName { get; set; }
    public List<string> TokenIcon { get; set; }
    public long Date { get; set; }
    public long LockUpPeriod { get; set; }
    public string PoolId { get; set; }
}

public enum PoolTypeEnums
{
    Points = 0,
    Token = 1,
    Lp = 2,
    All = -1,
}