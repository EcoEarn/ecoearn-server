using EcoEarnServer.Common;
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

    public async Task<GrainResultDto<PointsStakeRewardsSumDto>> CreateOrUpdateAsync(PointsStakeRewardsSumDto input)
    {
        State = _objectMapper.Map<PointsStakeRewardsSumDto, PointsStakeRewardsSumState>(input);
        State.Id = this.GetPrimaryKeyString();
        State.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();

        await WriteStateAsync();

        return new GrainResultDto<PointsStakeRewardsSumDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointsStakeRewardsSumState, PointsStakeRewardsSumDto>(State)
        };
    }
}