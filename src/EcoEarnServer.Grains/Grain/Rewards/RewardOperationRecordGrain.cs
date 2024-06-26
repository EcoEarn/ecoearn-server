using EcoEarnServer.Common;
using EcoEarnServer.Grains.State;
using EcoEarnServer.Rewards;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Grains.Grain.Rewards;

public interface IRewardOperationRecordGrain : IGrainWithStringKey
{
    Task<GrainResultDto<RewardOperationRecordDto>> CreateAsync(RewardOperationRecordDto input);
    Task<RewardOperationRecordDto> GetAsync();
    Task<GrainResultDto<RewardOperationRecordDto>> EndedAsync();
    Task<GrainResultDto<RewardOperationRecordDto>> CancelAsync();
}

public class RewardOperationRecordGrain : Grain<RewardOperationRecordState>, IRewardOperationRecordGrain
{
    private readonly IObjectMapper _objectMapper;

    public RewardOperationRecordGrain(IObjectMapper objectMapper)
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

    public async Task<GrainResultDto<RewardOperationRecordDto>> CreateAsync(RewardOperationRecordDto input)
    {
        State = _objectMapper.Map<RewardOperationRecordDto, RewardOperationRecordState>(input);
        State.Id = this.GetPrimaryKeyString();
        State.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();

        await WriteStateAsync();

        return new GrainResultDto<RewardOperationRecordDto>
        {
            Success = true,
            Data = _objectMapper.Map<RewardOperationRecordDto, RewardOperationRecordState>(State)
        };
    }

    public async Task<RewardOperationRecordDto> GetAsync()
    {
        return State.Id == null ? null : _objectMapper.Map<RewardOperationRecordState, RewardOperationRecordDto>(State);
    }

    public async Task<GrainResultDto<RewardOperationRecordDto>> EndedAsync()
    {
        State.ExecuteStatus = ExecuteStatus.Ended;
        await WriteStateAsync();
        return new GrainResultDto<RewardOperationRecordDto>
        {
            Success = true,
            Data = _objectMapper.Map<RewardOperationRecordDto, RewardOperationRecordState>(State)
        };
    }

    public async Task<GrainResultDto<RewardOperationRecordDto>> CancelAsync()
    {
        State.ExecuteStatus = ExecuteStatus.Cancel;
        await WriteStateAsync();
        return new GrainResultDto<RewardOperationRecordDto>
        {
            Success = true,
            Data = _objectMapper.Map<RewardOperationRecordDto, RewardOperationRecordState>(State)
        };
    }
}