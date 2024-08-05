using EcoEarnServer.Common;
using EcoEarnServer.Grains.State;
using EcoEarnServer.PointsPool;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Grains.Grain.PointsPool;

public interface IPointsPoolClaimRecordGrain : IGrainWithStringKey
{
    Task<GrainResultDto<PointsPoolClaimRecordDto>> CreateAsync(PointsPoolClaimRecordDto input);
    Task<PointsPoolClaimRecordDto> GetAsync();
    Task<GrainResultDto<PointsPoolClaimRecordDto>> ClaimedAsync();
}

public class PointsPoolClaimRecordGrain : Grain<PointsPoolClaimRecordState>, IPointsPoolClaimRecordGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<PointsPoolClaimRecordGrain> _logger;

    public PointsPoolClaimRecordGrain(IObjectMapper objectMapper, ILogger<PointsPoolClaimRecordGrain> logger)
    {
        _objectMapper = objectMapper;
        _logger = logger;
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

    public async Task<GrainResultDto<PointsPoolClaimRecordDto>> CreateAsync(PointsPoolClaimRecordDto input)
    {
        State = _objectMapper.Map<PointsPoolClaimRecordDto, PointsPoolClaimRecordState>(input);
        State.Id = this.GetPrimaryKeyString();
        State.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();

        await WriteStateAsync();

        return new GrainResultDto<PointsPoolClaimRecordDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointsPoolClaimRecordDto, PointsPoolClaimRecordState>(State)
        };
    }

    public async Task<PointsPoolClaimRecordDto> GetAsync()
    {
        _logger.LogInformation("get PointsPoolClaimRecord Id {id}", State.Id);
        return State.Id == null ? null : _objectMapper.Map<PointsPoolClaimRecordState, PointsPoolClaimRecordDto>(State);
    }

    public async Task<GrainResultDto<PointsPoolClaimRecordDto>> ClaimedAsync()
    {
        State.ClaimStatus = ClaimStatus.Claimed;
        await WriteStateAsync();
        return new GrainResultDto<PointsPoolClaimRecordDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointsPoolClaimRecordDto, PointsPoolClaimRecordState>(State)
        };
    }
}