using System;
using System.Threading.Tasks;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Common;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface IStateProvider
{
    Task<bool> CheckStateAsync(string key, bool isSettle = false);
    Task SetStateAsync(string key, bool state, int hours = 25);
}

public class StateProvider : AbpRedisCache, IStateProvider, ISingletonDependency
{
    private readonly IDistributedCacheSerializer _serializer;
    private readonly ILogger<StateProvider> _logger;


    public StateProvider(IOptions<RedisCacheOptions> optionsAccessor,
        ILogger<StateProvider> logger, IDistributedCacheSerializer serializer) : base(optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
    }

    public async Task<bool> CheckStateAsync(string key, bool isSettle = false)
    {
        await ConnectAsync();
        var redisValue = await RedisDatabase.StringGetAsync(key);
        _logger.LogInformation("get snapshot key: {key}, state: {state}", key, redisValue);
        if (!redisValue.HasValue)
        {
            return false;
        }

        var stateDto = _serializer.Deserialize<StateDto>(redisValue);
        if (isSettle)
        {
            return stateDto.State && DateTime.UtcNow.ToUtcMilliSeconds() - stateDto.FinishTime > 5 * 60 * 1000;
        }
        return stateDto.State;
    }

    public async Task SetStateAsync(string key, bool state, int hours = 25)
    {
        await ConnectAsync();
        var stateDto = new StateDto
        {
            State = state,
            FinishTime = DateTime.UtcNow.ToUtcMilliSeconds()
        };
        await RedisDatabase.StringSetAsync(key, _serializer.Serialize(stateDto), TimeSpan.FromHours(hours));
        _logger.LogInformation("set snapshot state success");
    }
}