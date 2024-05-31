using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.PointsStakeRewards;
using Nest;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface ISettlePointsRewardsProvider
{
    Task<List<PointsSnapshotIndex>> GetSnapshotListAsync(string snapshotDate, int skipCount, int maxResultCount);

    Task<List<PointsStakeRewardsIndex>> GetEndedRewardsListAsync(string endSettleDate, int skipCount,
        int maxResultCount);
}

public class SettlePointsRewardsProvider : ISettlePointsRewardsProvider, ISingletonDependency
{
    private readonly INESTRepository<PointsSnapshotIndex, string> _repository;
    private readonly INESTRepository<PointsStakeRewardsIndex, string> _rewardsRepository;

    public SettlePointsRewardsProvider(INESTRepository<PointsSnapshotIndex, string> repository,
        INESTRepository<PointsStakeRewardsIndex, string> rewardsRepository)
    {
        _repository = repository;
        _rewardsRepository = rewardsRepository;
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

    public async Task<List<PointsStakeRewardsIndex>> GetEndedRewardsListAsync(string endSettleDate, int skipCount,
        int maxResultCount)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<PointsStakeRewardsIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.EndSettleDate).Value(endSettleDate)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.PeriodState).Value(PeriodState.InPeriod)));


        QueryContainer Filter(QueryContainerDescriptor<PointsStakeRewardsIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var result = await _rewardsRepository.GetListAsync(Filter, skip: skipCount, limit: maxResultCount,
            sortType: SortOrder.Ascending, sortExp: o => o.CreateTime);

        return result.Item2;
    }
}