using EcoEarnServer.Common;
using EcoEarnServer.Grains.State;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Grains.Grain.PointsSnapshot;

public interface IPointsSnapshotGrain : IGrainWithStringKey
{
    Task<GrainResultDto<PointsSnapshotDto>> CreateAsync(PointsSnapshotDto input);
}

public class PointsSnapshotGrain : Grain<PointsSnapshotState>, IPointsSnapshotGrain
{
    private readonly IObjectMapper _objectMapper;

    public PointsSnapshotGrain(IObjectMapper objectMapper)
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

    public async Task<GrainResultDto<PointsSnapshotDto>> CreateAsync(PointsSnapshotDto input)
    {
        State = _objectMapper.Map<PointsSnapshotDto, PointsSnapshotState>(input);
        State.Id = this.GetPrimaryKeyString();
        State.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();

        await WriteStateAsync();

        return new GrainResultDto<PointsSnapshotDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointsSnapshotState, PointsSnapshotDto>(State)
        };
    }
}