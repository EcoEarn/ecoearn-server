using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface IStateProvider
{
    Task<bool> CheckStateAsync(string key);
    Task SetStateAsync(string key, bool state);
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

    public async Task<bool> CheckStateAsync(string key)
    {
        await ConnectAsync();
        var redisValue = await RedisDatabase.StringGetAsync(key);
        _logger.LogInformation("get snapshot key: {key}, state: {state}", key, redisValue);
        return redisValue.HasValue && _serializer.Deserialize<bool>(redisValue);
    }

    public async Task SetStateAsync(string key, bool state)
    {
        await ConnectAsync();
        await RedisDatabase.StringSetAsync(key, _serializer.Serialize(state), TimeSpan.FromHours(25));
        _logger.LogInformation("set snapshot state success");
    }
}