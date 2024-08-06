using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using EcoEarnServer.Background.Dtos;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Background.Provider;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Common;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Background.Services;

public interface IPointsSnapshotService
{
    Task ExecuteAsync();
}

public class PointsSnapshotService : IPointsSnapshotService, ISingletonDependency
{
    private readonly IPointsSnapshotProvider _pointsSnapshotProvider;
    private readonly IStateProvider _stateProvider;
    private readonly IAbpDistributedLock _distributedLock;

    private readonly ILogger<PointsSnapshotService> _logger;

    private readonly SelfIncreaseRateOptions _selfIncreaseRateOptions;
    private readonly PointsSnapshotOptions _pointsSnapshotOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly ISnapshotGeneratorService _snapshotGeneratorService;
    private readonly ILarkAlertProvider _larkAlertProvider;

    private const string LockKeyPrefix = "EcoEarnServer:PointsSnapshot:Lock:";


    public PointsSnapshotService(IPointsSnapshotProvider pointsSnapshotProvider, IAbpDistributedLock distributedLock,
        ILogger<PointsSnapshotService> logger, IStateProvider stateProvider,
        IOptionsSnapshot<SelfIncreaseRateOptions> selfIncreaseRateOptions,
        IOptionsSnapshot<PointsSnapshotOptions> pointsSnapshotOptions, IObjectMapper objectMapper,
        ISnapshotGeneratorService snapshotGeneratorService, ILarkAlertProvider larkAlertProvider)
    {
        _pointsSnapshotProvider = pointsSnapshotProvider;
        _distributedLock = distributedLock;
        _logger = logger;
        _stateProvider = stateProvider;
        _objectMapper = objectMapper;
        _snapshotGeneratorService = snapshotGeneratorService;
        _larkAlertProvider = larkAlertProvider;
        _pointsSnapshotOptions = pointsSnapshotOptions.Value;
        _selfIncreaseRateOptions = selfIncreaseRateOptions.Value;
    }

    public async Task ExecuteAsync()
    {
        await using var handle = await _distributedLock.TryAcquireAsync(name: LockKeyPrefix);

        if (handle == null)
        {
            _logger.LogWarning("do not get lock, keys already exits.");
            return;
        }

        if (await _stateProvider.CheckStateAsync(StateGeneratorHelper.GenerateSnapshotKey()))
        {
            _logger.LogInformation("today has already created points snapshot.");
            return;
        }

        try
        {
            //generate points snapshot
            var pointsSumList = await GetPointsSumListAsync();

            _logger.LogInformation("need to snapshot count .{count}", pointsSumList.Count);
            await PointsBatchSnapshotAsync(pointsSumList);

            var larkAlertDto =
                BuildLarkAlertParam(pointsSumList.Count, 0, DateTime.UtcNow.ToString("yyyy-MM-dd"), true);
            await _larkAlertProvider.SendLarkAlertAsync(larkAlertDto);

            await _stateProvider.SetStateAsync(StateGeneratorHelper.GenerateSnapshotKey(), true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CreatePointsSnapshot fail.");
            await _stateProvider.SetStateAsync(StateGeneratorHelper.GenerateSnapshotKey(), false);
            await _larkAlertProvider.SendLarkFailAlertAsync(e.Message);
        }
    }

    private async Task PointsBatchSnapshotAsync(List<PointsListDto> pointsSumList)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var recurCount = pointsSumList.Count / _pointsSnapshotOptions.BatchSnapshotCount + 1;
        for (var i = 0; i < recurCount; i++)
        {
            var skipCount = _pointsSnapshotOptions.BatchSnapshotCount * i;
            var list = pointsSumList.Skip(skipCount).Take(_pointsSnapshotOptions.BatchSnapshotCount).ToList();

            if (list.IsNullOrEmpty()) return;
            BackgroundJob.Enqueue(() => _snapshotGeneratorService.BatchGenerateSnapshotAsync(list, today));
            await Task.Delay(_pointsSnapshotOptions.TaskDelayMilliseconds);
        }
    }

    private async Task<List<PointsListDto>> GetPointsSumListAsync()
    {
        var result = new List<PointsListDto>();
        var now = DateTime.UtcNow;
        var startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
        var startOfDayTimestamp = startOfDay.ToUtcMilliSeconds();

        var pointsSumList = await _pointsSnapshotProvider.GetPointsSumListAsync();
        //group by address and calculate points sum

        _logger.LogInformation("get points sum list count. {count}", pointsSumList.Count);
        var addressRelationShipDic = await GetRelationShipAsync(pointsSumList);
        var addressPointsGroups = pointsSumList.GroupBy(x => x.Address)
            .Select(g => new
            {
                Address = g.Key,
                Points = g.ToList()
            })
            .ToList();

        foreach (var addressPointsGroup in addressPointsGroups)
        {
            var address = addressPointsGroup.Address;
            var dappId = addressPointsGroup.Points.FirstOrDefault()?.DappId;
            var flag = addressRelationShipDic.TryGetValue(address, out var addressRelationShip);
            var newPointList = new PointsListDto
            {
                Address = address,
                UpdateTime = startOfDayTimestamp,
                DappId = dappId
            };
            foreach (var pointsListDto in addressPointsGroup.Points)
            {
                newPointList.FirstSymbolAmount = (BigInteger.Parse(newPointList.FirstSymbolAmount) +
                                                  BigInteger.Parse(pointsListDto.FirstSymbolAmount)).ToString();
                newPointList.ThirdSymbolAmount = (BigInteger.Parse(newPointList.ThirdSymbolAmount) +
                                                  BigInteger.Parse(pointsListDto.ThirdSymbolAmount)).ToString();
                newPointList.FourSymbolAmount = (BigInteger.Parse(newPointList.FourSymbolAmount) +
                                                 BigInteger.Parse(pointsListDto.FourSymbolAmount)).ToString();
                newPointList.FiveSymbolAmount = (BigInteger.Parse(newPointList.FiveSymbolAmount) +
                                                 BigInteger.Parse(pointsListDto.FiveSymbolAmount)).ToString();
                newPointList.SixSymbolAmount = (BigInteger.Parse(newPointList.SixSymbolAmount) +
                                                BigInteger.Parse(pointsListDto.SixSymbolAmount)).ToString();
                newPointList.SevenSymbolAmount = (BigInteger.Parse(newPointList.SevenSymbolAmount) +
                                                  BigInteger.Parse(pointsListDto.SevenSymbolAmount)).ToString();
                newPointList.EightSymbolAmount = (BigInteger.Parse(newPointList.EightSymbolAmount) +
                                                  BigInteger.Parse(pointsListDto.EightSymbolAmount)).ToString();
                newPointList.NineSymbolAmount = (BigInteger.Parse(newPointList.NineSymbolAmount) +
                                                 BigInteger.Parse(pointsListDto.NineSymbolAmount)).ToString();
                newPointList.TenSymbolAmount = (BigInteger.Parse(newPointList.TenSymbolAmount) +
                                                BigInteger.Parse(pointsListDto.TenSymbolAmount)).ToString();
                newPointList.ElevenSymbolAmount = (BigInteger.Parse(newPointList.ElevenSymbolAmount) +
                                                   BigInteger.Parse(pointsListDto.ElevenSymbolAmount)).ToString();
                newPointList.TwelveSymbolAmount = (BigInteger.Parse(newPointList.TwelveSymbolAmount) +
                                                   BigInteger.Parse(pointsListDto.TwelveSymbolAmount)).ToString();
                switch (pointsListDto.Role)
                {
                    case OperatorRole.Inviter:
                        var settleInviterMilliSeconds = startOfDayTimestamp - pointsListDto.UpdateTime;
                        var settleInviterPoints = flag
                            ? new BigInteger(addressRelationShip.InviterKolFollowerNum) *
                              new BigInteger(_selfIncreaseRateOptions.InviterKolFollowerRate) *
                              new BigInteger(settleInviterMilliSeconds)
                            : BigInteger.Parse("0");
                        newPointList.SecondSymbolAmount = (BigInteger.Parse(newPointList.SecondSymbolAmount) +
                                                           BigInteger.Parse(pointsListDto.SecondSymbolAmount) +
                                                           settleInviterPoints).ToString();
                        break;
                    case OperatorRole.Kol:
                        var settleKolMilliSeconds = startOfDayTimestamp - pointsListDto.UpdateTime;
                        var settleKolPoints = flag
                            ? (new BigInteger(addressRelationShip.KolFollowerNum) *
                               new BigInteger(_selfIncreaseRateOptions.KolFollowerRate) +
                               new BigInteger(addressRelationShip.KolFollowerInviteeNum) *
                               new BigInteger(_selfIncreaseRateOptions.KolFollowerInviteeRate)) *
                              new BigInteger(settleKolMilliSeconds)
                            : BigInteger.Parse("0");
                        newPointList.SecondSymbolAmount = (BigInteger.Parse(newPointList.SecondSymbolAmount) +
                                                           BigInteger.Parse(pointsListDto.SecondSymbolAmount) +
                                                           settleKolPoints).ToString();
                        break;
                    case OperatorRole.User:
                        var settleUserMilliSeconds = startOfDayTimestamp - pointsListDto.UpdateTime;
                        var settleUserPoints = flag
                            ? (new BigInteger(addressRelationShip.InviteeNum) *
                               new BigInteger(_selfIncreaseRateOptions.InviteeRate) +
                               new BigInteger(addressRelationShip.SecondInviteeNum) *
                               new BigInteger(_selfIncreaseRateOptions.SecondInviteeRate)) *
                              new BigInteger(settleUserMilliSeconds) 
                            : BigInteger.Parse("0");
                        newPointList.SecondSymbolAmount = (BigInteger.Parse(newPointList.SecondSymbolAmount) +
                                                           BigInteger.Parse(pointsListDto.SecondSymbolAmount) +
                                                           settleUserPoints).ToString();
                        break;
                    case OperatorRole.All:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (_pointsSnapshotOptions.ElevenSymbolSubAddressDic.TryGetValue(address, out var dto))
            {
                newPointList.ElevenSymbolAmount = (BigInteger.Parse(newPointList.ElevenSymbolAmount) -
                                                   BigInteger.Parse(dto.Amount)).ToString();
            }

            if (_pointsSnapshotOptions.NineSymbolContractAddressList.Contains(address))
            {
                newPointList.NineSymbolAmount = "0";
            }

            result.Add(newPointList);
        }

        var larkAlertDto = BuildLarkAlertParam(pointsSumList.Count, result.Count, startOfDay.ToString("yyyy-MM-dd"));
        await _larkAlertProvider.SendLarkAlertAsync(larkAlertDto);
        return result;
    }

    private LarkAlertDto BuildLarkAlertParam(int count, int resultCount, string date, bool isSnapshotEnd = false)
    {
        var msg = isSnapshotEnd
            ? $"Points Snapshot Generate End({date}).\nGenerate Points Snapshot Count: {count}"
            : $"Points Snapshot Generate Start({date}). \nPoints List Count: {count}. \n Distinct Address List Count: {resultCount}";
        var content = new Dictionary<string, string>()
        {
            ["text"] = msg,
        };
        return new LarkAlertDto
        {
            MsgType = LarkAlertMsgType.Text,
            Content = JsonConvert.SerializeObject(content)
        };
    }

    private async Task<Dictionary<string, RelationshipDto>> GetRelationShipAsync(List<PointsListDto> pointsSumList)
    {
        var addressList = pointsSumList.Select(pointSum => pointSum.Address).Distinct().ToList();
        var relationShipList = await _pointsSnapshotProvider.GetRelationShipAsync(addressList);
        return relationShipList.ToDictionary(relationShip => relationShip.Address, relationShip => relationShip);
    }
}