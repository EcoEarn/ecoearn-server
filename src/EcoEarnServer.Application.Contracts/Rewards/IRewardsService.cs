using System.Threading.Tasks;
using EcoEarnServer.Rewards.Dtos;
using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Rewards;

public interface IRewardsService
{
    Task<PagedResultDto<RewardsListDto>> GetRewardsListAsync(GetRewardsListInput input);
    Task<RewardsAggregationDto> GetRewardsAggregationAsync(GetRewardsAggregationInput input);
}