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

    public override async Task OnActivateAsync()
    {
        await ReadStateAsync();
        await base.OnActivateAsync();
    }

    public override async Task OnDeactivateAsync()
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync();
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