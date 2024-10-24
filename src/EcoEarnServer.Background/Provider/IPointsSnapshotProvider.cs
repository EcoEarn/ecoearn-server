using System;
using System.Collections.Generic;
using System.Linq;
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
        var apiInfo = new ApiInfo(HttpMethod.Post, "api/app/points/all/list");
        var lastId = "";
        var lastBlockHeight = 0;
        var input = new GetPointsSumListInput()
        {
            MaxResultCount = _pointsSnapshotOptions.BatchQueryCount,
            LastBlockHeight = lastBlockHeight,
            LastId = lastId
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

                var pointsListDto = list.Last();
                input.SkipCount += count;
                input.LastId = pointsListDto.Id;
                input.LastBlockHeight = pointsListDto.BlockHeight;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "get points list from points server fail.");
                throw;
            }
        } while (!list.IsNullOrEmpty());

        return res;
    }

    public async Task<List<RelationshipDto>> GetRelationShipAsync(List<string> addressList)
    {
        var res = new List<RelationshipDto>();
        var apiInfo = new ApiInfo(HttpMethod.Post, "api/app/points/relationship");
        var input = new GetRelationShipInput()
        {
            //EndTime = DateTime.UtcNow.Date,
            AddressList = addressList,
            ChainId = "tDVW"
        };
        try
        {
            var resp = await _httpProvider.InvokeAsync<CommonResponseDto<List<RelationshipDto>>>(
                _pointsSnapshotOptions.PointsServerBaseUrl, apiInfo,
                body: JsonConvert.SerializeObject(input, JsonSerializerSettings));
            res = resp.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get points list from points server fail.");
            throw;
        }

        return res;
    }
}