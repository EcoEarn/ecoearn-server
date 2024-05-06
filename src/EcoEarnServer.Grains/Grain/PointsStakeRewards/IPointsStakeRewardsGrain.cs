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

    public Task<GrainResultDto<PointsStakeRewardsDto>> CreateOrUpdateAsync(PointsStakeRewardsDto input)
    {
        throw new NotImplementedException();
    }
}