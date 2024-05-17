using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Background.Provider;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Common;
using EcoEarnServer.Grains.Grain.TokenPool;
using Hangfire;
using Microsoft.Extensions.Logging;
using Orleans;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Background.Services;

public interface IUpdatePoolStakeSumService
{
    Task UpdatePoolStakeSumAsync(int optionsUpdatePoolStakeSumWorkerDelayPeriod);
}

public class UpdatePoolStakeSumService : IUpdatePoolStakeSumService, ISingletonDependency
{
    private readonly IUpdatePoolStakeSumProvider _updatePoolStakeSumProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<UpdatePoolStakeSumService> _logger;
    private readonly IClusterClient _clusterClient;

    public UpdatePoolStakeSumService(IUpdatePoolStakeSumProvider updatePoolStakeSumProvider, IObjectMapper objectMapper,
        ILogger<UpdatePoolStakeSumService> logger, IClusterClient clusterClient)
    {
        _updatePoolStakeSumProvider = updatePoolStakeSumProvider;
        _objectMapper = objectMapper;
        _logger = logger;
        _clusterClient = clusterClient;
    }

    public async Task UpdatePoolStakeSumAsync(int optionsUpdatePoolStakeSumWorkerDelayPeriod)
    {
        var allStakedInfoList = await GetAllStakedInfoListAsync();

        foreach (var stakeInfo in allStakedInfoList)
        {
            var executeTme = stakeInfo.StakedTime + stakeInfo.Period * 1000 - optionsUpdatePoolStakeSumWorkerDelayPeriod;
            var grain = _clusterClient.GetGrain<ITokenStakeUpdateWorkerGrain>(stakeInfo.StakeId);
            var dto = await grain.GetAsync();
            if (dto != null)
            {
                if (dto.ExecuteTime == executeTme)
                {
                    _logger.LogWarning("UpdatePoolStakeSumAsync job exist.");
                    continue;
                }

                BackgroundJob.Delete(dto.JobId);
            }

            var delay = executeTme - DateTime.UtcNow.ToUtcMilliSeconds();
            var jobId = BackgroundJob.Schedule(
                () => _updatePoolStakeSumProvider.ExecuteUpdateStakeAsync(stakeInfo.StakeId),
                TimeSpan.FromMilliseconds(delay < 0 ? 1000 : delay));

            var tokenStakeUpdateWorkerDto = new TokenStakeUpdateWorkerDto
            {
                Id = stakeInfo.StakeId,
                StakeId = stakeInfo.StakeId,
                JobId = jobId,
                ExecuteTime = executeTme
            };
            var grainResultDto = await grain.CreateOrUpdateAsync(tokenStakeUpdateWorkerDto);
            if (!grainResultDto.Success)
            {
                _logger.LogError(
                    "update TokenStakeUpdateWorkerDto fail, message:{message}, id:{id}",
                    grainResultDto.Message, stakeInfo.StakeId);
            }
        }
    }

    private async Task<List<StakedInfoIndexerDto>> GetAllStakedInfoListAsync()
    {
        var res = new List<StakedInfoIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<StakedInfoIndexerDto> list;
        do
        {
            list = await _updatePoolStakeSumProvider.GetStakedInfoListAsync(skipCount, maxResultCount);
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < maxResultCount)
            {
                break;
            }

            skipCount += count;
        } while (!list.IsNullOrEmpty());

        return res.Where(x => x.StakedTime + x.Period * 1000 > DateTime.UtcNow.ToUtcMilliSeconds())
            .ToList();
    }
}