using System.Globalization;
using EcoEarnServer.Common;
using EcoEarnServer.Grains.State;
using EcoEarnServer.StakingSettlePoints;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Grains.Grain.StakingPoints;

public interface IAddressStakingSettlePointsGrain : IGrainWithStringKey
{
    Task<GrainResultDto<AddressStakingSettlePointsDto>> CreateOrUpdateAsync(AddressStakingSettlePointsDto input);
}

public class AddressStakingSettlePointsGrain : Grain<AddressStakingSettlePointsState>, IAddressStakingSettlePointsGrain
{
    private readonly IObjectMapper _objectMapper;

    public AddressStakingSettlePointsGrain(IObjectMapper objectMapper)
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

    public async Task<GrainResultDto<AddressStakingSettlePointsDto>> CreateOrUpdateAsync(
        AddressStakingSettlePointsDto input)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (string.IsNullOrEmpty(State.Id))
        {
            State = _objectMapper.Map<AddressStakingSettlePointsDto, AddressStakingSettlePointsState>(input);
            State.Id = this.GetPrimaryKeyString();
            State.CreateTime = now;
            State.UpdateTime = now;
        }
        else
        {
            var oldPoints = decimal.Parse(State.Points);
            var newPoints = oldPoints + decimal.Parse(input.Points);
            State.Points = newPoints.ToString(CultureInfo.InvariantCulture);
            var dappIdDic = input.DappPoints
                .ToDictionary(x => x.DappId, x => x.Points);
            var dappNewPointsList = new List<StakingSettlePointsDto>();
            foreach (var dappSettlePoints in State.DappPoints)
            {
                var dappId = dappSettlePoints.DappId;
                if (!dappIdDic.TryGetValue(dappId, out var addPoints))
                {
                    dappNewPointsList.Add(dappSettlePoints);
                    continue;
                }

                var dappNewPoints = decimal.Parse(addPoints) + decimal.Parse(dappSettlePoints.Points);
                dappNewPointsList.Add(new StakingSettlePointsDto()
                {
                    DappId = dappId,
                    Points = dappNewPoints.ToString(CultureInfo.InvariantCulture)
                });
            }

            State.DappPoints = dappNewPointsList;
            State.UpdateTime = now;
        }

        await WriteStateAsync();

        return new GrainResultDto<AddressStakingSettlePointsDto>
        {
            Success = true,
            Data = _objectMapper.Map<AddressStakingSettlePointsState, AddressStakingSettlePointsDto>(State)
        };
    }
}