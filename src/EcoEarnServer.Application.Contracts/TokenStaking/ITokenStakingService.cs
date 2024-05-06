using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.TokenStaking.Dtos;

namespace EcoEarnServer.TokenStaking;

public interface ITokenStakingService
{
    Task<List<TokenPoolsDto>> GetTokenPoolsAsync(GetTokenPoolsInput input);
}