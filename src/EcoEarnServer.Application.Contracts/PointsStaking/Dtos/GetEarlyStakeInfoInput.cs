using EcoEarnServer.Rewards.Dtos;

namespace EcoEarnServer.PointsStaking.Dtos;

public class GetEarlyStakeInfoInput
{
    public string TokenName { get; set; }
    public string Address { get; set; }
    public string ChainId { get; set; }
    public PoolTypeEnums PoolType { get; set; }
    public double Rate {get; set;}
}