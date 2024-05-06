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
[Route("api/app/token/staking")]
public class TokenStakingController
{
    private readonly ITokenStakingService _tokenStakingService;

    public TokenStakingController(ITokenStakingService tokenStakingService)
    {
        _tokenStakingService = tokenStakingService;
    }

    [HttpPost("pools")]
    public async Task<List<TokenPoolsDto>> GetTokenPoolsAsync(GetTokenPoolsInput input)
    {
        return await _tokenStakingService.GetTokenPoolsAsync(input);
    }
}