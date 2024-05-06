using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.TokenStaking;

public class TokenStakingService : ITokenStakingService, ISingletonDependency
{
    private readonly ITokenStakingProvider _tokenStakingProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TokenStakingService> _logger;

    public TokenStakingService(ITokenStakingProvider tokenStakingProvider, IObjectMapper objectMapper,
        ILogger<TokenStakingService> logger)
    {
        _tokenStakingProvider = tokenStakingProvider;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task<List<TokenPoolsDto>> GetTokenPoolsAsync(GetTokenPoolsInput input)
    {
        var tokenPoolsIndexerDtos = await _tokenStakingProvider.GetTokenPoolsAsync();
        return _objectMapper.Map<List<TokenPoolsIndexerDto>, List<TokenPoolsDto>>(tokenPoolsIndexerDtos);
    }
}