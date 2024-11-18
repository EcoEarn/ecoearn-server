using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.TokenStaking.Dtos;

namespace EcoEarnServer.TokenStaking;

public interface ITokenStakingService
{
    Task<TokenPoolsResult> GetTokenPoolsAsync(GetTokenPoolsInput input);
    Task<long> GetTokenPoolStakedSumAsync(GetTokenPoolStakedSumInput input);
    Task<List<EarlyStakeInfoDto>> GetStakedInfoAsync(GetEarlyStakeInfoInput input);
    Task<List<TokenPoolInfoDto>> GetTokenPoolInfosAsync(GetTokenPoolsInput input);
}