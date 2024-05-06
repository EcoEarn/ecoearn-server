using EcoEarnServer.Grains.State;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Grains.Grain.PointsPool;

public interface IPointsPoolAddressStakeGrain : IGrainWithStringKey
{
    Task<GrainResultDto<PointsPoolAddressStakeDto>> CreateOrUpdateAsync(PointsPoolAddressStakeDto input);
}

public class PointsPoolAddressStakeGrain : Grain<PointsPoolAddressStakeState>, IPointsPoolAddressStakeGrain
{
    private readonly IObjectMapper _objectMapper;

    public PointsPoolAddressStakeGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public Task<GrainResultDto<PointsPoolAddressStakeDto>> CreateOrUpdateAsync(PointsPoolAddressStakeDto input)
    {
        throw new NotImplementedException();
    }
}