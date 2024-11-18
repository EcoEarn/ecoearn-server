using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.TokenStaking;
using EcoEarnServer.TokenStaking.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace EcoEarnServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("TokenStakingController")]
[Route("api/app/simple/staking")]
public class TokenStakingController : EcoEarnServerController
{
    private readonly ITokenStakingService _tokenStakingService;

    public TokenStakingController(ITokenStakingService tokenStakingService)
    {
        _tokenStakingService = tokenStakingService;
    }

    [HttpPost("pools")]
    public async Task<TokenPoolsResult> GetTokenPoolsAsync(GetTokenPoolsInput input)
    {
        return await _tokenStakingService.GetTokenPoolsAsync(input);
    }

    [HttpPost("staked/sum")]
    public async Task<long> GetTokenPoolStakedSumAsync(GetTokenPoolStakedSumInput input)
    {
        return await _tokenStakingService.GetTokenPoolStakedSumAsync(input);
    }
    
    [HttpPost("pool/infos")]
    public async Task<List<TokenPoolInfoDto>> GetTokenPoolInfosAsync(GetTokenPoolsInput input)
    {
        return await _tokenStakingService.GetTokenPoolInfosAsync(input);
    }
}