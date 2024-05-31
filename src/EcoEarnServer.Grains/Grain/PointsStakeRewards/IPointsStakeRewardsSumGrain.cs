using System.Globalization;
using EcoEarnServer.Grains.State;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Grains.Grain.PointsStakeRewards;

public interface IPointsStakeRewardsSumGrain : IGrainWithStringKey
{
    Task<GrainResultDto<PointsStakeRewardsSumDto>> CreateOrUpdateAsync(PointsStakeRewardsSumDto input);
    Task<PointsStakeRewardsSumDto> GetAsync();
    Task<GrainResultDto<PointsStakeRewardsSumDto>> ClaimRewardsAsync(string amount);
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

    public async Task<PointsStakeRewardsSumDto> GetAsync()
    {
        return State.Id == null ? null : _objectMapper.Map<PointsStakeRewardsSumState, PointsStakeRewardsSumDto>(State);
    }

    public async Task<GrainResultDto<PointsStakeRewardsSumDto>> ClaimRewardsAsync(string amount)
    {
        var oldRewards = decimal.Parse(State.Rewards);
        var newRewards = oldRewards + decimal.Parse(amount);
        State.Rewards = newRewards.ToString(CultureInfo.InvariantCulture);
        
        await WriteStateAsync();
        return new GrainResultDto<PointsStakeRewardsSumDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointsStakeRewardsSumState, PointsStakeRewardsSumDto>(State)
        };
    }

    public async Task<GrainResultDto<PointsStakeRewardsSumDto>> CreateOrUpdateAsync(PointsStakeRewardsSumDto input)
    {
        State = _objectMapper.Map<PointsStakeRewardsSumDto, PointsStakeRewardsSumState>(input);
        State.Id = this.GetPrimaryKeyString();

        await WriteStateAsync();
        return new GrainResultDto<PointsStakeRewardsSumDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointsStakeRewardsSumState, PointsStakeRewardsSumDto>(State)
        };
    }
}