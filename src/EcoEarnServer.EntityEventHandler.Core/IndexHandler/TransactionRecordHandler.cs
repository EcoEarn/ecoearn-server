using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.TransactionRecord;
using Microsoft.Extensions.Logging;
using Nest;
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
            index.IsFirstTransaction = await IsFirstTransaction(eventData.Address);
            index.Id = Guid.NewGuid().ToString();
            await _repository.AddOrUpdateAsync(index);
            _logger.LogDebug("HandleEventAsync TransactionRecordEto success.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventAsync TransactionRecordEto fail. {Message}",
                JsonConvert.SerializeObject(eventData));
        }
    }

    private async Task<bool> IsFirstTransaction(string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TransactionRecordIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Terms(i => i.Field(f => f.Address).Terms(address)));

        QueryContainer Filter(QueryContainerDescriptor<TransactionRecordIndex> f) => f.Bool(b => b.Must(mustQuery));
        var countResponse = await _repository.CountAsync(Filter);
        return countResponse.Count == 0;
    }
}