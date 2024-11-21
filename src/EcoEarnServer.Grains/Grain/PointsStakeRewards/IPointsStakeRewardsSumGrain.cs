using System.Globalization;
using System.Numerics;
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

    public async Task<GrainResultDto<PointsStakeRewardsSumDto>> CreateOrUpdateAsync(PointsStakeRewardsSumDto input)
    {
        if (string.IsNullOrEmpty(State.Id))
        {
            State = _objectMapper.Map<PointsStakeRewardsSumDto, PointsStakeRewardsSumState>(input);
            State.Id = this.GetPrimaryKeyString();
            State.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();
        }
        else
        {
            var oldRewards = decimal.Parse(State.Rewards);
            var newRewards = oldRewards + decimal.Parse(input.Rewards);
            State.Rewards = newRewards.ToString(CultureInfo.InvariantCulture);
        }

        await WriteStateAsync();

        return new GrainResultDto<PointsStakeRewardsSumDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointsStakeRewardsSumState, PointsStakeRewardsSumDto>(State)
        };
    }
}