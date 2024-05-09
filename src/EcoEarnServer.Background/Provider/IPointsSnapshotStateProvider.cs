using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface IPointsSnapshotStateProvider
{
    Task<bool> CheckPointsSnapshotStateAsync();
    Task SetPointsSnapshotStateAsync(bool state);
}

public class PointsSnapshotStateProvider : AbpRedisCache, IPointsSnapshotStateProvider, ISingletonDependency
{
    private const string SnapshotStateRedisKeyPrefix = "EcoEarnServer:SnapshotState:";

    private readonly IDistributedCacheSerializer _serializer;
    private readonly ILogger<PointsSnapshotStateProvider> _logger;


    public PointsSnapshotStateProvider(IOptions<RedisCacheOptions> optionsAccessor,
        ILogger<PointsSnapshotStateProvider> logger, IDistributedCacheSerializer serializer) : base(optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
    }

    public async Task<bool> CheckPointsSnapshotStateAsync()
    {
        await ConnectAsync();
        var currentDate = DateTime.UtcNow.ToString("yyyyMMdd");
        var redisValue = await RedisDatabase.StringGetAsync(SnapshotStateRedisKeyPrefix + currentDate);
        _logger.LogInformation("get snapshot state: {state}", redisValue);
        return redisValue.HasValue && _serializer.Deserialize<bool>(redisValue);
    }

    public async Task SetPointsSnapshotStateAsync(bool state)
    {
        await ConnectAsync();
        var currentDate = DateTime.UtcNow.ToString("yyyyMMdd");
        await RedisDatabase.StringSetAsync(SnapshotStateRedisKeyPrefix + currentDate, state);
        _logger.LogInformation("set snapshot state success");
    }
}