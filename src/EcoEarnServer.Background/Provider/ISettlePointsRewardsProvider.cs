using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Common.Dtos;
using EcoEarnServer.Common.HttpClient;
using EcoEarnServer.PointsSnapshot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface ISettlePointsRewardsProvider
{
    Task<List<PointsSnapshotIndex>> GetSnapshotListAsync(string snapshotDate, int skipCount, int maxResultCount);
    Task<string> GetUnboundEvmAddressPointsAsync();
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

    public async Task<string> GetUnboundEvmAddressPointsAsync()
    {
        var apiInfo = new ApiInfo(HttpMethod.Get, "api/app/remain-point");

        try
        {
            var resp = await _httpProvider.InvokeAsync<CommonResponseDto<UnBoundEvmAddressAmountDto>>(
                _pointsSnapshotOptions.SchrodingerServerBaseUrl, apiInfo);
            return resp.Data.Amount;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get un bound evm address amount fail.");
            throw new UserFriendlyException("get un bound evm address amount fail.");
        }
    }
}