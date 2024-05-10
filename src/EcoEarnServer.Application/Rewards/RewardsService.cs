using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using EcoEarnServer.Common;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.Rewards.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Rewards;

public class RewardsService : IRewardsService, ISingletonDependency
{
    private readonly IRewardsProvider _rewardsProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<RewardsService> _logger;

    public RewardsService(IRewardsProvider rewardsProvider, IObjectMapper objectMapper, ILogger<RewardsService> logger)
    {
        _rewardsProvider = rewardsProvider;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task<List<RewardsListDto>> GetRewardsListAsync(GetRewardsListInput input)
    {
        var rewardsIndexerList = await _rewardsProvider.GetRewardsListAsync(input.PoolType, input.Address,
            input.SkipCount, input.MaxResultCount, filterUnlocked: input.FilterUnlocked);
        return _objectMapper.Map<List<RewardsListIndexerDto>, List<RewardsListDto>>(rewardsIndexerList);
    }

    public async Task<RewardsAggregationDto> GetRewardsAggregationAsync(GetRewardsAggregationInput input)
    {
        var address = input.Address;
        var rewardsList = await GetAllRewardsList(address);
        var poolTypeRewardDic = rewardsList
            .GroupBy(x => x.PoolType)
            .ToDictionary(g => g.Key, g => g.ToList());
        var rewardsAggregationDto = new RewardsAggregationDto();
        foreach (var keyValuePair in poolTypeRewardDic)
        {
            switch (keyValuePair.Key)
            {
                case PoolTypeEnums.Points:
                    rewardsAggregationDto.PointsPoolAgg =
                        await GetPointsPoolRewardsAggAsync(keyValuePair.Value, address);
                    break;
                case PoolTypeEnums.Token:
                    rewardsAggregationDto.TokenPoolAgg = await GetTokenPoolRewardsAggAsync(keyValuePair.Value, address);
                    break;
                case PoolTypeEnums.Lp:
                    rewardsAggregationDto.LpPoolAgg = await GetLpPoolRewardsAggAsync(keyValuePair.Value, address);
                    break;
            }
        }

        return rewardsAggregationDto;
    }

    private async Task<PointsPoolAggDto> GetPointsPoolRewardsAggAsync(List<RewardsListIndexerDto> list, string address)
    {
        var pointsPoolAggDto = new PointsPoolAggDto();
        if (list.IsNullOrEmpty())
        {
            return pointsPoolAggDto;
        }

        var stakeIds = list
            .Where(x => !string.IsNullOrEmpty(x.StakeId))
            .Select(x => x.StakeId)
            .Distinct().ToList();
        var unLockedStakeIds = await _rewardsProvider.GetUnLockedStakeIdsAsync(stakeIds, address);

        var stakingList = list
            .Where(x => x.EarlyStakeTime == 0 || unLockedStakeIds.Contains(x.StakeId))
            .ToList();

        var unlockList = stakingList
            .Where(x => DateTime.UtcNow.ToUtcMilliSeconds() > x.UnlockTime)
            .ToList();

        pointsPoolAggDto.Total = stakingList.Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num).ToString();
        pointsPoolAggDto.RewardsTotal = unlockList.Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num).ToString();
        pointsPoolAggDto.RewardsTokenName = stakingList.First().ClaimedSymbol;
        return pointsPoolAggDto;
    }

    private async Task<TokenPoolAggDto> GetTokenPoolRewardsAggAsync(List<RewardsListIndexerDto> list, string address)
    {
        return new TokenPoolAggDto();
    }

    private async Task<TokenPoolAggDto> GetLpPoolRewardsAggAsync(List<RewardsListIndexerDto> list, string address)
    {
        return new TokenPoolAggDto();
    }


    private async Task<List<RewardsListIndexerDto>> GetAllRewardsList(string address)
    {
        var res = new List<RewardsListIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<RewardsListIndexerDto> list;
        do
        {
            list = await _rewardsProvider.GetRewardsListAsync(PoolTypeEnums.All, address, skipCount, maxResultCount,
                filterWithdraw: true);
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