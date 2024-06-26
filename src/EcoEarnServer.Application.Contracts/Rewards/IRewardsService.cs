using System.Threading.Tasks;
using EcoEarnServer.Rewards.Dtos;
using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Rewards;

public interface IRewardsService
{
    Task<PagedResultDto<RewardsListDto>> GetRewardsListAsync(GetRewardsListInput input);
    Task<RewardsAggregationDto> GetRewardsAggregationAsync(GetRewardsAggregationInput input);
    Task<RewardsSignatureDto> RewardsWithdrawSignatureAsync(RewardsSignatureInput input);
    Task<string> RewardsWithdrawAsync(RewardsTransactionInput input);

    Task<RewardsSignatureDto> EarlyStakeSignatureAsync(RewardsSignatureInput input);
    Task<string> EarlyStakeAsync(RewardsTransactionInput input);
    Task<RewardsSignatureDto> AddLiquiditySignatureAsync(RewardsSignatureInput input);
    Task<string> AddLiquidityAsync(RewardsTransactionInput input);
    Task<bool> CancelSignatureAsync(RewardsSignatureInput input);
}