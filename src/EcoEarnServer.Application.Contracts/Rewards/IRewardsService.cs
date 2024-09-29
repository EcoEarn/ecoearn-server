using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.Common.Dtos;
using EcoEarnServer.Rewards.Dtos;
using Volo.Abp.Application.Dtos;
using FilterItemDto = EcoEarnServer.Rewards.Dtos.FilterItemDto;

namespace EcoEarnServer.Rewards;

public interface IRewardsService
{
    Task<PagedResultDto<RewardsListDto>> GetRewardsListAsync(GetRewardsListInput input);
    Task<List<RewardsAggregationDto>> GetRewardsAggregationAsync(GetRewardsAggregationInput input);
    Task<RewardsSignatureDto> RewardsWithdrawSignatureAsync(RewardsSignatureInput input);
    Task<string> RewardsWithdrawAsync(RewardsTransactionInput input);

    Task<RewardsSignatureDto> EarlyStakeSignatureAsync(RewardsSignatureInput input);
    Task<string> EarlyStakeAsync(RewardsTransactionInput input);
    Task<RewardsSignatureDto> AddLiquiditySignatureAsync(RewardsSignatureInput input);
    Task<string> AddLiquidityAsync(RewardsTransactionInput input);
    Task<bool> CancelSignatureAsync(RewardsSignatureInput input);
    Task<RewardsSignatureDto> LiquidityStakeSignatureAsync(LiquiditySignatureInput input);
    Task<string> LiquidityStakeAsync(RewardsTransactionInput input);
    Task<RewardsSignatureDto> RemoveLiquiditySignatureAsync(LiquiditySignatureInput input);
    Task<string> RemoveLiquidityAsync(RewardsTransactionInput input);
    Task<List<FilterItemDto>> GetFilterItemsAsync();
    Task TransactionRecordAsync(TransactionRecordDto input);
    Task<bool> TransactionResultAsync(long transactionBlockHeight, string chainId);
}