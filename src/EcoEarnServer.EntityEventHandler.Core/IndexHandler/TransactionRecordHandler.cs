using System;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.TransactionRecord;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.EntityEventHandler.Core.IndexHandler;

public class TransactionRecordHandler : IDistributedEventHandler<TransactionRecordEto>,
    ITransientDependency
{
    private readonly INESTRepository<TransactionRecordIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TransactionRecordHandler> _logger;

    public TransactionRecordHandler(IObjectMapper objectMapper, ILogger<TransactionRecordHandler> logger,
        INESTRepository<TransactionRecordIndex, string> repository)
    {
        _objectMapper = objectMapper;
        _logger = logger;
        _repository = repository;
    }

    public async Task HandleEventAsync(TransactionRecordEto eventData)
    {
        try
        {
            var index = _objectMapper.Map<TransactionRecordEto, TransactionRecordIndex>(eventData);
            await _repository.AddOrUpdateAsync(index);
            _logger.LogDebug("HandleEventAsync TransactionRecordEto success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync TransactionRecordEto fail. {Message}",
                JsonConvert.SerializeObject(eventData));
        }
    }
}