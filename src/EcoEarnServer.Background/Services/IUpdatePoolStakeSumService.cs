using System;
using System.Threading.Tasks;
using EcoEarnServer.Background.Provider;
using Microsoft.Extensions.Logging;
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

    public UpdatePoolStakeSumService(IUpdatePoolStakeSumProvider updatePoolStakeSumProvider, IObjectMapper objectMapper,
        ILogger<UpdatePoolStakeSumService> logger)
    {
        _updatePoolStakeSumProvider = updatePoolStakeSumProvider;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task UpdatePoolStakeSumAsync(int optionsUpdatePoolStakeSumWorkerDelayPeriod)
    {
        await GetAllStakedInfoListAsync();
        
        throw new NotImplementedException();
    }

    private async Task GetAllStakedInfoListAsync()
    {
        throw new NotImplementedException();
    }
}