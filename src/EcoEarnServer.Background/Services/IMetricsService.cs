using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Background.Provider;
using EcoEarnServer.Background.Services.Dtos;
using EcoEarnServer.Common;
using EcoEarnServer.Metrics;
using EcoEarnServer.Options;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using EcoEarnServer.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;

namespace EcoEarnServer.Background.Services;

public interface IMetricsService
{
    Task GenerateMetricsAsync();
}

public class MetricsService : IMetricsService, ISingletonDependency
{
    private const string LockKeyPrefix = "EcoEarnServer:MetricsGenerate:Lock:";
    
    private readonly ITokenStakingProvider _tokenStakingProvider;
    private readonly IPriceProvider _priceProvider;
    private readonly LpPoolRateOptions _lpPoolRateOptions;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IUserInformationProvider _userInformationProvider;
    private readonly ILogger<MetricsService> _logger;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IStateProvider _stateProvider;

    public MetricsService(ITokenStakingProvider tokenStakingProvider, IPriceProvider priceProvider,
        IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions, IDistributedEventBus distributedEventBus,
        IUserInformationProvider userInformationProvider, ILogger<MetricsService> logger,
        IAbpDistributedLock distributedLock, IStateProvider stateProvider)
    {
        _tokenStakingProvider = tokenStakingProvider;
        _priceProvider = priceProvider;
        _distributedEventBus = distributedEventBus;
        _userInformationProvider = userInformationProvider;
        _logger = logger;
        _distributedLock = distributedLock;
        _stateProvider = stateProvider;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
    }

    public async Task GenerateMetricsAsync()
    {
        await using var handle = await _distributedLock.TryAcquireAsync(name: LockKeyPrefix);

        if (handle == null)
        {
            _logger.LogWarning("do not get lock, keys already exits.");
            return;
        }

        // if (await _stateProvider.CheckStateAsync(StateGeneratorHelper.GenerateMetricsKey()))
        // {
        //     _logger.LogInformation("today has already generate metrics.");
        //     return;
        // }
        
        var rateDic = new Dictionary<string, double>();
        var evenDataList = new List<BizMetricsEto>();
        var nowDateTime = DateTime.UtcNow;
        var now = nowDateTime.ToUtcMilliSeconds();
        var nowDate = nowDateTime.ToString("yyyy-MM-dd");
        var allTokenStakedList = await _tokenStakingProvider.GetTokenPoolStakedInfoListAsync(new List<string>());

        var allPools = await _tokenStakingProvider.GetTokenPoolsAsync(new GetTokenPoolsInput
        {
            PoolType = PoolTypeEnums.All
        });
        var poolIdDic = allPools.GroupBy(x => x.PoolId).ToDictionary(g => g.Key, g => g.First());
        var stakePriceDtoList = new List<StakePriceDto>();
        foreach (var tokenPoolStakedInfoDto in allTokenStakedList)
        {
            if (!poolIdDic.TryGetValue(tokenPoolStakedInfoDto.PoolId, out var poolInfo))
            {
                continue;
            }

            var currencyPair = $"{poolInfo.TokenPoolConfig.StakingToken.ToUpper()}_USDT";
            var feeRate = _lpPoolRateOptions.LpPoolRateDic.TryGetValue(
                poolInfo.TokenPoolConfig.StakeTokenContract,
                out var poolRate)
                ? poolRate
                : 0;
            double rate;
            var key = GuidHelper.GenerateId(poolInfo.PoolType.ToString(), currencyPair);
            if (poolInfo.PoolType == PoolTypeEnums.Token)
            {
                rate = await _priceProvider.GetGateIoPriceAsync(currencyPair);
            }
            else
            {
                rate = await _priceProvider.GetLpPriceAsync(poolInfo.TokenPoolConfig.StakingToken, feeRate);
                key = GuidHelper.GenerateId(poolInfo.PoolType.ToString(), poolInfo.TokenPoolConfig.StakingToken,
                    poolInfo.TokenPoolConfig.StakeTokenContract);
            }

            rateDic.TryAdd(key, rate);
            var dto = new StakePriceDto()
            {
                PoolId = tokenPoolStakedInfoDto.PoolId,
                Amount = tokenPoolStakedInfoDto.TotalStakedAmount,
                UsdAmount =
                    (double.Parse(tokenPoolStakedInfoDto.TotalStakedAmount) * rate).ToString(CultureInfo
                        .InvariantCulture),
                Rate = rate.ToString(CultureInfo.InvariantCulture)
            };
            stakePriceDtoList.Add(dto);
        }

        var usdAmount = (stakePriceDtoList.Sum(x => double.Parse(x.UsdAmount)) / 100000000).ToString(CultureInfo.InvariantCulture);

        var platformStakedUsdAmount = new BizMetricsEto()
        {
            Id = GuidHelper.GenerateId(nowDate,BizType.PlatformStakedUsdAmount.ToString()),
            BizNumber = usdAmount,
            CreateTime = now,
            BizType = BizType.PlatformStakedUsdAmount
        };


        var userCount = await _userInformationProvider.GetUserCount();
        var registerCount = new BizMetricsEto()
        {
            Id = GuidHelper.GenerateId(nowDate,BizType.RegisterCount.ToString()),
            BizNumber = userCount.ToString(),
            CreateTime = now,
            BizType = BizType.RegisterCount
        };

        var allStakeInfoList = await GetAllStakeInfoList();
        var poolTypeStakeInfoDic = allStakeInfoList
            .GroupBy(x => x.PoolType)
            .ToDictionary(g => g.Key, g => g.ToList());
        if (poolTypeStakeInfoDic.TryGetValue(PoolTypeEnums.Token, out var tokenStakedInfo))
        {
            var tokenStakeAddressCount = tokenStakedInfo.Select(x => x.Account).Distinct().Count();
            var tokenStakeAmount = tokenStakedInfo.SelectMany(x => x.SubStakeInfos)
                .Sum(x => x.StakedAmount + x.EarlyStakedAmount) / 100000000;
            var tokenStakeUsdAmount = "0";
            var key = GuidHelper.GenerateId(PoolTypeEnums.Token.ToString(),
                $"{tokenStakedInfo.First().StakingToken.ToUpper()}_USDT");
            if (rateDic.TryGetValue(key, out var rate))
            {
                tokenStakeUsdAmount =
                    (double.Parse(tokenStakeAmount.ToString()) * rate).ToString(CultureInfo.InvariantCulture);
            }

            var tokenStakedAddressCount = new BizMetricsEto()
            {
                Id = GuidHelper.GenerateId(nowDate,BizType.TokenStakedAddressCount.ToString()),
                BizNumber = tokenStakeAddressCount.ToString(),
                CreateTime = now,
                BizType = BizType.TokenStakedAddressCount
            };

            var tokenStakedAmount = new BizMetricsEto()
            {
                Id = GuidHelper.GenerateId(nowDate,BizType.TokenStakedAmount.ToString()),
                BizNumber = tokenStakeAmount.ToString(),
                CreateTime = now,
                BizType = BizType.TokenStakedAmount
            };

            var tokenStakedUsdAmount = new BizMetricsEto()
            {
                Id = GuidHelper.GenerateId(nowDate,BizType.TokenStakedUsdAmount.ToString()),
                BizNumber = tokenStakeUsdAmount,
                CreateTime = now,
                BizType = BizType.TokenStakedUsdAmount
            };

            evenDataList.Add(tokenStakedAddressCount);
            evenDataList.Add(tokenStakedAmount);
            evenDataList.Add(tokenStakedUsdAmount);
        }

        if (poolTypeStakeInfoDic.TryGetValue(PoolTypeEnums.Lp, out var lpStakedInfo))
        {
            var lpStakeAddressCount = lpStakedInfo.Select(x => x.Account).Distinct().Count();
            var lpStakeAmount = lpStakedInfo.SelectMany(x => x.SubStakeInfos)
                .Sum(x => x.StakedAmount + x.EarlyStakedAmount) / 100000000;
            double lpStakeUsdAmount = 0;

            foreach (var tokenStakedIndexerDto in lpStakedInfo)
            {
                if (!poolIdDic.TryGetValue(tokenStakedIndexerDto.PoolId, out var poolInfo))
                {
                    continue;
                }

                var key = GuidHelper.GenerateId(PoolTypeEnums.Lp.ToString(), tokenStakedIndexerDto.StakingToken,
                    poolInfo.TokenPoolConfig.StakeTokenContract);
                if (!rateDic.TryGetValue(key, out var rate))
                {
                    continue;
                }

                var sum = tokenStakedIndexerDto.SubStakeInfos.Sum(x => x.StakedAmount + x.EarlyStakedAmount) / 100000000;
                lpStakeUsdAmount += sum * rate;
            }

            var lpStakedAddressCount = new BizMetricsEto()
            {
                Id = GuidHelper.GenerateId(nowDate,BizType.LpStakedAddressCount.ToString()),
                BizNumber = lpStakeAddressCount.ToString(),
                CreateTime = now,
                BizType = BizType.LpStakedAddressCount
            };

            var lpStakedAmount = new BizMetricsEto()
            {
                Id = GuidHelper.GenerateId(nowDate,BizType.LpStakedAmount.ToString()),
                BizNumber = lpStakeAmount.ToString(),
                CreateTime = now,
                BizType = BizType.LpStakedAmount
            };

            var lpStakedUsdAmount = new BizMetricsEto()
            {
                Id = GuidHelper.GenerateId(nowDate,BizType.LpStakedUsdAmount.ToString()),
                BizNumber = lpStakeUsdAmount.ToString(CultureInfo.InvariantCulture),
                CreateTime = now,
                BizType = BizType.LpStakedUsdAmount
            };

            evenDataList.Add(lpStakedAddressCount);
            evenDataList.Add(lpStakedAmount);
            evenDataList.Add(lpStakedUsdAmount);
        }

        evenDataList.Add(platformStakedUsdAmount);
        evenDataList.Add(registerCount);

        await _distributedEventBus.PublishAsync(new BizMetricsListEto
        {
            EventDataList = evenDataList
        });
        
        //await _stateProvider.SetStateAsync(StateGeneratorHelper.GenerateMetricsKey(), true);
    }


    private async Task<List<TokenStakedIndexerDto>> GetAllStakeInfoList()
    {
        var res = new List<TokenStakedIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<TokenStakedIndexerDto> list;
        do
        {
            list = await _tokenStakingProvider.GetStakedInfoListAsync("", "", new List<string>(), skipCount,
                maxResultCount);
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < maxResultCount)
            {
                break;
            }

            skipCount += count;
        } while (!list.IsNullOrEmpty());

        return res;
    }
}