using EcoEarnServer.Grains.State;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Grains.Grain.PointsStakeRewards;

public interface IPointsStakeRewardsSumGrain : IGrainWithStringKey
{
    Task<GrainResultDto<PointsStakeRewardsSumDto>> CreateOrUpdateAsync(PointsStakeRewardsSumDto input);
}

public class PointsStakeRewardsSumGrain : Grain<PointsStakeRewardsSumState>, IPointsStakeRewardsSumGrain
{
    private readonly IObjectMapper _objectMapper;

    public PointsStakeRewardsSumGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public Task<GrainResultDto<PointsStakeRewardsSumDto>> CreateOrUpdateAsync(PointsStakeRewardsSumDto input)
    {
        throw new NotImplementedException();
    }
}