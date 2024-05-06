using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using EcoEarnServer.Background.Dtos;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Common;
using EcoEarnServer.Common.Dtos;
using EcoEarnServer.Common.HttpClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface IPointsSnapshotProvider
{
    Task<List<PointsListDto>> GetPointsSumListAsync();
    Task<List<RelationshipDto>> GetRelationShipAsync(List<string> addressList);
}

public class PointsSnapshotProvider : IPointsSnapshotProvider, ISingletonDependency
{
    private readonly IHttpProvider _httpProvider;
    private readonly PointsSnapshotOptions _pointsSnapshotOptions;
    private readonly ILogger<PointsSnapshotProvider> _logger;

    private static readonly JsonSerializerSettings JsonSerializerSettings = JsonSettingsBuilder.New()
        .IgnoreNullValue()
        .WithCamelCasePropertyNamesResolver()
        .WithAElfTypesConverters()
        .Build();

    public PointsSnapshotProvider(IHttpProvider httpProvider,
        IOptionsSnapshot<PointsSnapshotOptions> pointsSnapshotOptions, ILogger<PointsSnapshotProvider> logger)
    {
        _httpProvider = httpProvider;
        _logger = logger;
        _pointsSnapshotOptions = pointsSnapshotOptions.Value;
    }

    public async Task<List<PointsListDto>> GetPointsSumListAsync()
    {
        var res = new List<PointsListDto>();
        var apiInfo = new ApiInfo(HttpMethod.Post, "api/app/points/list");
        var input = new GetPointsSumListInput()
        {
            BeforeTime = DateTime.UtcNow.Date,
            SkipCount = 0,
            MaxResultCount = _pointsSnapshotOptions.BatchQueryCount
        };
        List<PointsListDto> list;

        do
        {
            try
            {
                var resp = await _httpProvider.InvokeAsync<CommonResponseDto<List<PointsListDto>>>(
                    _pointsSnapshotOptions.PointsServerBaseUrl, apiInfo,
                    body: JsonConvert.SerializeObject(input, JsonSerializerSettings));
                list = resp.Data;
                var count = list.Count;
                res.AddRange(list);
                if (list.IsNullOrEmpty() || count < _pointsSnapshotOptions.BatchQueryCount)
                {
                    break;
                }

                input.SkipCount += count;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "get points list from points server fail.");
                throw;
            }
        } while (!list.IsNullOrEmpty());

        return res;
    }

    public Task<List<RelationshipDto>> GetRelationShipAsync(List<string> addressList)
    {
        return null;
    }
}