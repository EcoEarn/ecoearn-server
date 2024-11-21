using EcoEarnServer.Common;
using EcoEarnServer.Grains.State;
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

    public async Task<GrainResultDto<PointsPoolAddressStakeDto>> CreateOrUpdateAsync(PointsPoolAddressStakeDto input)
    {
        State = _objectMapper.Map<PointsPoolAddressStakeDto, PointsPoolAddressStakeState>(input);
        State.Id = this.GetPrimaryKeyString();
        State.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();

        await WriteStateAsync();

        return new GrainResultDto<PointsPoolAddressStakeDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointsPoolAddressStakeState, PointsPoolAddressStakeDto>(State)
        };
    }
}