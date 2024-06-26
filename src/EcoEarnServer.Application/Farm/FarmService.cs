using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Farm.Dtos;
using EcoEarnServer.Farm.Provider;
using EcoEarnServer.Options;
using EcoEarnServer.Rewards.Provider;
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

    public FarmService(IFarmProvider farmProvider, IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions,
        IObjectMapper objectMapper, IPriceProvider priceProvider, IRewardsProvider rewardsProvider)
    {
        _farmProvider = farmProvider;
        _objectMapper = objectMapper;
        _priceProvider = priceProvider;
        _rewardsProvider = rewardsProvider;
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

        if (unLockedStakeIds.IsNullOrEmpty())
        {
            return new List<LiquidityInfoDto>();
        }

        
        var tokenAddressDic = liquidityInfoIndexerDtos
            .Where(x => unLockedStakeIds.Contains(x.StakeId))
            .GroupBy(x => x.TokenAddress)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<LiquidityInfoDto>();
        foreach (var entity in tokenAddressDic)
        {
            
            var liquidityInfoDto = new LiquidityInfoDto();
            liquidityInfoDto.LpSymbol = entity.Value.First()?.LpSymbol;
            liquidityInfoDto.TokenASymbol = entity.Value.First()?.TokenASymbol;
            liquidityInfoDto.TokenBSymbol = entity.Value.First()?.TokenBSymbol;
            liquidityInfoDto.Banlance = entity.Value.Sum(x => x.LpAmount).ToString();
            liquidityInfoDto.RewardSymbol = entity.Value.First()?.RewardSymbol;
            liquidityInfoDto.Rate = _lpPoolRateOptions.LpPoolRateDic.TryGetValue(
                entity.Key,
                out var poolRate)
                ? poolRate
                : 0;
            var lpPrice = await _priceProvider.GetLpPriceAsync("", liquidityInfoDto.Rate, liquidityInfoDto.TokenASymbol,
                liquidityInfoDto.TokenBSymbol);
            liquidityInfoDto.Value =
                (double.Parse(liquidityInfoDto.Banlance) * lpPrice).ToString(CultureInfo.InvariantCulture);
            liquidityInfoDto.TokenAAmount = entity.Value.Sum(x => x.TokenAAmount).ToString();
            liquidityInfoDto.TokenBAmount = entity.Value.Sum(x => x.TokenBAmount).ToString();
            liquidityInfoDto.LiquidityIds = entity.Value.Select(x => x.LiquidityId).ToList();

            result.Add(liquidityInfoDto);
        }

        return result;
    }

    public async Task<List<LiquidityInfoDto>> GetMarketLiquidityListAsync(GetMyLiquidityListInput input)
    {
        var awakenLiquidityInfoList = await _farmProvider.GetAwakenLiquidityInfoAsync("EECOTEST-102", "EECOTEST-4");
        var myLiquidityList = await GetMyLiquidityListAsync(input);
        var rateDic = myLiquidityList.ToDictionary(x => x.Rate, x => x);
        var result = new List<LiquidityInfoDto>();
        foreach (var lpPriceItemDto in awakenLiquidityInfoList)
        {
            var liquidityInfoDto = _objectMapper.Map<LpPriceItemDto, LiquidityInfoDto>(lpPriceItemDto);
            liquidityInfoDto.Icons = new List<string>();
            if (rateDic.TryGetValue(liquidityInfoDto.Rate, out var myLiquidity))
            {
                liquidityInfoDto.LiquidityIds = myLiquidity.LiquidityIds;
                liquidityInfoDto.RewardSymbol = myLiquidity.RewardSymbol;
                liquidityInfoDto.EcoEarnTokenAAmount = myLiquidity.TokenAAmount;
                liquidityInfoDto.EcoEarnTokenBAmount = myLiquidity.TokenBAmount;
                liquidityInfoDto.EcoEarnBanlance = myLiquidity.Banlance;
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
}