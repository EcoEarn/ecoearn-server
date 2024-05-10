using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.Rewards.Dtos;

namespace EcoEarnServer.Rewards;

public interface IRewardsService
{
    Task<List<RewardsListDto>> GetRewardsListAsync(GetRewardsListInput input);
    Task<RewardsAggregationDto> GetRewardsAggregationAsync(GetRewardsAggregationInput input);
}