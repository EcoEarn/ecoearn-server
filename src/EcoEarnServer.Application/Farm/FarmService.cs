using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Common;
using EcoEarnServer.Farm.Dtos;
using EcoEarnServer.Farm.Provider;
using EcoEarnServer.Options;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.Rewards.Provider;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Linq;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Farm;

public class FarmService : IFarmService, ISingletonDependency
{
    private readonly IFarmProvider _farmProvider;
    private readonly LpPoolRateOptions _lpPoolRateOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IPriceProvider _priceProvider;
    private readonly IRewardsProvider _rewardsProvider;
    private readonly ITokenStakingProvider _tokenStakingProvider;

    public FarmService(IFarmProvider farmProvider, IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions,
        IObjectMapper objectMapper, IPriceProvider priceProvider, IRewardsProvider rewardsProvider,
        ITokenStakingProvider tokenStakingProvider)
    {
        _farmProvider = farmProvider;
        _objectMapper = objectMapper;
        _priceProvider = priceProvider;
        _rewardsProvider = rewardsProvider;
        _tokenStakingProvider = tokenStakingProvider;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
    }

    public async Task<List<LiquidityInfoDto>> GetMyLiquidityListAsync(GetMyLiquidityListInput input)
    {
        var liquidityInfoIndexerDtos = await GetAllLiquidityList(input.Address);
        var stakeIds = liquidityInfoIndexerDtos
            .Where(x => !string.IsNullOrEmpty(x.StakeId))
            .Select(x => x.StakeId)
            .Distinct()
            .ToList();
        var unLockedStakeIds = await _rewardsProvider.GetUnLockedStakeIdsAsync(stakeIds, input.Address);

        var tokenAddressDic = liquidityInfoIndexerDtos
            //.Where(x => unLockedStakeIds.Contains(x.StakeId))
            .GroupBy(x => x.TokenAddress)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<LiquidityInfoDto>();
        foreach (var entity in tokenAddressDic)
        {
            var liquidityInfoDto = new LiquidityInfoDto();
            liquidityInfoDto.LpSymbol = entity.Value.First()?.LpSymbol;
            liquidityInfoDto.TokenASymbol = entity.Value.First()?.TokenASymbol;
            liquidityInfoDto.TokenBSymbol = entity.Value.First()?.TokenBSymbol;
            liquidityInfoDto.Banlance =
                (decimal.Parse(entity.Value.Where(x => unLockedStakeIds.Contains(x.StakeId)).Sum(x => x.LpAmount)
                    .ToString()) / 100000000).ToString(CultureInfo.InvariantCulture);
            liquidityInfoDto.RewardSymbol = entity.Value.First()?.RewardSymbol;
            liquidityInfoDto.Rate = _lpPoolRateOptions.LpPoolRateDic.TryGetValue(
                entity.Key,
                out var poolRate)
                ? poolRate
                : 0;
            var lpPrice = await _priceProvider.GetLpPriceAsync("", liquidityInfoDto.Rate, liquidityInfoDto.TokenASymbol,
                liquidityInfoDto.TokenBSymbol);
            liquidityInfoDto.StakingAmount =
                (double.Parse(entity.Value.Where(x => !unLockedStakeIds.Contains(x.StakeId)).Sum(x => x.LpAmount)
                    .ToString()) / 100000000).ToString(CultureInfo.InvariantCulture);
            liquidityInfoDto.Value =
                ((double.Parse(liquidityInfoDto.Banlance) + double.Parse(liquidityInfoDto.StakingAmount)) * lpPrice).ToString(CultureInfo.InvariantCulture);
            liquidityInfoDto.TokenAAmount =
                (decimal.Parse(entity.Value.Sum(x => x.TokenAAmount).ToString()) / 100000000).ToString(CultureInfo
                    .InvariantCulture);
            liquidityInfoDto.TokenAUnStakingAmount =
                (decimal.Parse(entity.Value.Where(x => unLockedStakeIds.Contains(x.StakeId)).Sum(x => x.TokenAAmount)
                    .ToString()) / 100000000).ToString(CultureInfo.InvariantCulture);
            liquidityInfoDto.TokenBAmount =
                (decimal.Parse(entity.Value.Sum(x => x.TokenBAmount).ToString()) / 100000000).ToString(CultureInfo
                    .InvariantCulture);
            liquidityInfoDto.TokenBUnStakingAmount =
                (decimal.Parse(entity.Value.Where(x => unLockedStakeIds.Contains(x.StakeId)).Sum(x => x.TokenBAmount)
                    .ToString()) / 100000000).ToString(CultureInfo.InvariantCulture);
            liquidityInfoDto.LiquidityIds = entity.Value
                .Where(x => unLockedStakeIds.Contains(x.StakeId))
                .Select(x => x.LiquidityId).ToList();
            liquidityInfoDto.LpAmount = entity.Value
                .Where(x => unLockedStakeIds.Contains(x.StakeId))
                .Sum(x => x.LpAmount);
            result.Add(liquidityInfoDto);
        }

        return result;
    }

    public async Task<List<MarketLiquidityInfoDto>> GetMarketLiquidityListAsync(GetMyLiquidityListInput input)
    {
        var myLiquidityList = await GetMyLiquidityListAsync(input);
        var rateDic = myLiquidityList.ToDictionary(x => x.Rate, x => x);
        var rates = _lpPoolRateOptions.LpPoolRateDic.Values.Select(x => x).ToList();
        var tokenPoolsIndexerDtos = await _tokenStakingProvider.GetTokenPoolsAsync(new GetTokenPoolsInput()
        {
            PoolType = PoolTypeEnums.Lp
        });
        var awakenLiquidityInfoList = new List<LpPriceItemDto>();
        foreach (var tokenPoolsIndexerDto in tokenPoolsIndexerDtos)
        {
            var (symbol0, symbol1) = LpSymbolHelper.GetLpSymbols(tokenPoolsIndexerDto.TokenPoolConfig.StakingToken);
            var awakenLiquidityInfos = await _farmProvider.GetAwakenLiquidityInfoAsync(symbol0, symbol1);
            foreach (var lpPriceItemDto in awakenLiquidityInfos)
            {
                if (rates.Contains(lpPriceItemDto.FeeRate))
                {
                    awakenLiquidityInfoList.Add(lpPriceItemDto);
                }
            }
        }

        var result = new List<MarketLiquidityInfoDto>();
        foreach (var lpPriceItemDto in awakenLiquidityInfoList)
        {
            var liquidityInfoDto = _objectMapper.Map<LpPriceItemDto, MarketLiquidityInfoDto>(lpPriceItemDto);
            liquidityInfoDto.Icons = new List<string>();
            if (rateDic.TryGetValue(liquidityInfoDto.Rate, out var myLiquidity))
            {
                liquidityInfoDto.LiquidityIds = myLiquidity.LiquidityIds;
                liquidityInfoDto.LpAmount = myLiquidity.LpAmount;
                liquidityInfoDto.RewardSymbol = myLiquidity.RewardSymbol;
                liquidityInfoDto.EcoEarnTokenAAmount = myLiquidity.TokenAAmount;
                liquidityInfoDto.EcoEarnTokenBAmount = myLiquidity.TokenBAmount;
                liquidityInfoDto.EcoEarnBanlance = myLiquidity.Banlance;
                liquidityInfoDto.StakingAmount = myLiquidity.StakingAmount;
                liquidityInfoDto.EcoEarnTokenAUnStakingAmount = myLiquidity.TokenAUnStakingAmount;
                liquidityInfoDto.EcoEarnTokenBUnStakingAmount = myLiquidity.TokenBUnStakingAmount;
            }

            result.Add(liquidityInfoDto);
        }

        return result;
    }

    private async Task<List<LiquidityInfoIndexerDto>> GetAllLiquidityList(string address)
    {
        var res = new List<LiquidityInfoIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<LiquidityInfoIndexerDto> list;
        do
        {
            var listIndexerResult = await _farmProvider.GetLiquidityInfoAsync(new List<string>(), address,
                LpStatus.Added, skipCount, maxResultCount);
            list = listIndexerResult;
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

    private async Task<List<RewardsListIndexerDto>> GetAllRewardsList(string address, PoolTypeEnums poolType,
        List<string> liquidityIds = null)
    {
        var res = new List<RewardsListIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<RewardsListIndexerDto> list;
        do
        {
            var rewardsListIndexerResult = await _rewardsProvider.GetRewardsListAsync(poolType, address,
                skipCount, maxResultCount, liquidityIds: liquidityIds);
            list = rewardsListIndexerResult.Data;
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