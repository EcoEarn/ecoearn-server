using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Common.HttpClient;
using EcoEarnServer.PointsSnapshot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface ISettlePointsRewardsProvider
{
    Task<List<PointsSnapshotIndex>> GetSnapshotListAsync(string snapshotDate, int skipCount, int maxResultCount);
}

public class SettlePointsRewardsProvider : ISettlePointsRewardsProvider, ISingletonDependency
{
    private readonly INESTRepository<PointsSnapshotIndex, string> _repository;
    private readonly PointsSnapshotOptions _pointsSnapshotOptions;
    private readonly IHttpProvider _httpProvider;
    private readonly ILogger<SettlePointsRewardsProvider> _logger;

    public SettlePointsRewardsProvider(INESTRepository<PointsSnapshotIndex, string> repository,
        ILogger<SettlePointsRewardsProvider> logger, IHttpProvider httpProvider,
        IOptionsSnapshot<PointsSnapshotOptions> pointsSnapshotOptions)
    {
        _repository = repository;
        _logger = logger;
        _httpProvider = httpProvider;
        _pointsSnapshotOptions = pointsSnapshotOptions.Value;
    }

    public async Task<List<PointsSnapshotIndex>> GetSnapshotListAsync(string snapshotDate, int skipCount,
        int maxResultCount)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<PointsSnapshotIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.SnapshotDate).Value(snapshotDate)));


        QueryContainer Filter(QueryContainerDescriptor<PointsSnapshotIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var result = await _repository.GetListAsync(Filter, skip: skipCount, limit: maxResultCount,
            sortType: SortOrder.Ascending, sortExp: o => o.CreateTime);

        return result.Item2;
    }
}