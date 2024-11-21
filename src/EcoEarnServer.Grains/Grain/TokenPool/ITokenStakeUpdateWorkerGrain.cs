using EcoEarnServer.Common;
using EcoEarnServer.Grains.State;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Grains.Grain.TokenPool;

public interface ITokenStakeUpdateWorkerGrain : IGrainWithStringKey
{
    Task<GrainResultDto<TokenStakeUpdateWorkerDto>> CreateOrUpdateAsync(TokenStakeUpdateWorkerDto input);
    Task<TokenStakeUpdateWorkerDto> GetAsync();
}

public class TokenStakeUpdateWorkerGrain : Grain<TokenStakeUpdateWorkerState>, ITokenStakeUpdateWorkerGrain
{
    private readonly IObjectMapper _objectMapper;

    public TokenStakeUpdateWorkerGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        await ReadStateAsync();
        await base.OnActivateAsync(token);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken token)
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync(reason, token);
    }


    public async Task<GrainResultDto<TokenStakeUpdateWorkerDto>> CreateOrUpdateAsync(TokenStakeUpdateWorkerDto input)
    {
        State = _objectMapper.Map<TokenStakeUpdateWorkerDto, TokenStakeUpdateWorkerState>(input);
        State.Id = this.GetPrimaryKeyString();
        State.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();
        
        await WriteStateAsync();

        return new GrainResultDto<TokenStakeUpdateWorkerDto>
        {
            Success = true,
            Data = _objectMapper.Map<TokenStakeUpdateWorkerState, TokenStakeUpdateWorkerDto>(State)
        };
    }

    public async Task<TokenStakeUpdateWorkerDto> GetAsync()
    {
        return State.Id == null
            ? null
            : _objectMapper.Map<TokenStakeUpdateWorkerState, TokenStakeUpdateWorkerDto>(State);
    }
}