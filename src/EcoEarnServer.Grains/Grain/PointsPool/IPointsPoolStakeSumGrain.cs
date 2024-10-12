using EcoEarnServer.Grains.State;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Grains.Grain.PointsPool;

public interface IPointsPoolStakeSumGrain : IGrainWithStringKey
{
    Task<GrainResultDto<PointsPoolStakeSumDto>> CreateOrUpdateAsync(PointsPoolStakeSumDto input);
}

public class PointsPoolStakeSumGrain : Grain<PointsPoolStakeSumState>, IPointsPoolStakeSumGrain
{
    private readonly IObjectMapper _objectMapper;

    public PointsPoolStakeSumGrain(IObjectMapper objectMapper)
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

    public async Task<GrainResultDto<PointsPoolStakeSumDto>> CreateOrUpdateAsync(PointsPoolStakeSumDto input)
    {
        State = _objectMapper.Map<PointsPoolStakeSumDto, PointsPoolStakeSumState>(input);
        State.Id = this.GetPrimaryKeyString();

        await WriteStateAsync();

        return new GrainResultDto<PointsPoolStakeSumDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointsPoolStakeSumState, PointsPoolStakeSumDto>(State)
        };
    }
}