using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.PointsSnapshot;
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

    public SettlePointsRewardsProvider(INESTRepository<PointsSnapshotIndex, string> repository)
    {
        _repository = repository;
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