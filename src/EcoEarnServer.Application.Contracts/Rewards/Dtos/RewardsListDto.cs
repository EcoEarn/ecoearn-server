namespace EcoEarnServer.Rewards.Dtos;

public class RewardsListDto
{
    public PoolTypeEnums PooType { get; set; }
    public string Rewards { get; set; }
    public string TokenName { get; set; }
    public string TokenIcon { get; set; }
    public long Date { get; set; }
    public long LockUpPeriod { get; set; }
}

public enum PoolTypeEnums
{
    Points,
    Token,
    Lp
}