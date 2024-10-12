using EcoEarnServer.Common;
using EcoEarnServer.Grains.State;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Grains.Grain.PointsStakeRewards;

public interface IPointsStakeRewardsGrain : IGrainWithStringKey
{
    Task<GrainResultDto<PointsStakeRewardsDto>> CreateOrUpdateAsync(PointsStakeRewardsDto input);
}

public class PointsStakeRewardsGrain : Grain<PointsStakeRewardsState>, IPointsStakeRewardsGrain
{
    private readonly IObjectMapper _objectMapper;

    public PointsStakeRewardsGrain(IObjectMapper objectMapper)
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

    public async Task<GrainResultDto<PointsStakeRewardsDto>> CreateOrUpdateAsync(PointsStakeRewardsDto input)
    {
        State = _objectMapper.Map<PointsStakeRewardsDto, PointsStakeRewardsState>(input);
        State.Id = this.GetPrimaryKeyString();
        State.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();

        await WriteStateAsync();

        return new GrainResultDto<PointsStakeRewardsDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointsStakeRewardsState, PointsStakeRewardsDto>(State)
        };
    }
}