using System;
using System.Threading.Tasks;
using EcoEarnServer.Common.Dtos;
using EcoEarnServer.TransactionRecord;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Common.TransactionRecord;

public interface ITransactionRecordProvider
{
    Task SaveTransactionRecordAsync(TransactionRecordDto dto);
}

public class TransactionRecordProvider : ITransactionRecordProvider, ISingletonDependency
{
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILogger<TransactionRecordProvider> _logger;
    private readonly IObjectMapper _objectMapper;

    public TransactionRecordProvider(IDistributedEventBus distributedEventBus,
        ILogger<TransactionRecordProvider> logger, IObjectMapper objectMapper)
    {
        _distributedEventBus = distributedEventBus;
        _logger = logger;
        _objectMapper = objectMapper;
    }

    public async Task SaveTransactionRecordAsync(TransactionRecordDto dto)
    {
        dto.CreateTime = DateTime.UtcNow.ToUtcMilliSeconds();
        _logger.LogInformation("save transaction record: {record}", JsonConvert.SerializeObject(dto));
        await _distributedEventBus.PublishAsync(
            _objectMapper.Map<TransactionRecordDto, TransactionRecordEto>(dto));
    }
}