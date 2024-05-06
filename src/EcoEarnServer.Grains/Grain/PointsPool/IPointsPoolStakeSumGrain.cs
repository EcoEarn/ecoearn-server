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

    public Task<GrainResultDto<PointsPoolStakeSumDto>> CreateOrUpdateAsync(PointsPoolStakeSumDto input)
    {
        throw new NotImplementedException();
    }
}