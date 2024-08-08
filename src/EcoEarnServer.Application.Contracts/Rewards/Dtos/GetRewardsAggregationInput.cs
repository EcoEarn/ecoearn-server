namespace EcoEarnServer.Rewards.Dtos;

public class GetRewardsAggregationInput
{
    public string Address { get; set; }
    public PoolTypeEnums PoolType { get; set; }
}