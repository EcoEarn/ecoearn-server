using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Farm.Dtos;
using EcoEarnServer.Farm.Provider;
using EcoEarnServer.Options;
using EcoEarnServer.TokenStaking.Provider;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Farm;

public class FarmService : IFarmService, ISingletonDependency
{
    private readonly IFarmProvider _farmProvider;
    private readonly LpPoolRateOptions _lpPoolRateOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IPriceProvider _priceProvider;

    public FarmService(IFarmProvider farmProvider, IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions,
        IObjectMapper objectMapper, IPriceProvider priceProvider)
    {
        _farmProvider = farmProvider;
        _objectMapper = objectMapper;
        _priceProvider = priceProvider;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
    }

    public async Task<List<LiquidityInfoDto>> GetMyLiquidityListAsync(GetMyLiquidityListInput input)
    {
        var liquidityInfoIndexerDtos = await GetAllLiquidityList(input.Address);
        var tokenAddressDic = liquidityInfoIndexerDtos
            .GroupBy(x => x.TokenAddress)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<LiquidityInfoDto>();
        foreach (var entity in tokenAddressDic)
        {
            var liquidityInfoDto = new LiquidityInfoDto();
            liquidityInfoDto.TokenASymbol = entity.Value.First()?.TokenASymbol;
            liquidityInfoDto.TokenBSymbol = entity.Value.First()?.TokenBSymbol;
            liquidityInfoDto.Banlance = entity.Value.Sum(x => x.LpAmount).ToString();
            liquidityInfoDto.Rate = _lpPoolRateOptions.LpPoolRateDic.TryGetValue(
                entity.Key,
                out var poolRate)
                ? poolRate
                : 0;
            var symbol = "ALP " + liquidityInfoDto.TokenASymbol + "-" + liquidityInfoDto.TokenBSymbol;
            var lpPrice = await _priceProvider.GetLpPriceAsync(symbol, liquidityInfoDto.Rate);
            liquidityInfoDto.Value =
                (double.Parse(liquidityInfoDto.Banlance) * lpPrice).ToString(CultureInfo.InvariantCulture);
            liquidityInfoDto.TokenAAmount =
                (double.Parse(liquidityInfoDto.Banlance) * lpPrice).ToString(CultureInfo.InvariantCulture);
            liquidityInfoDto.TokenBAmount =
                (double.Parse(liquidityInfoDto.Banlance) * lpPrice).ToString(CultureInfo.InvariantCulture);
            liquidityInfoDto.LiquidityIds = entity.Value.Select(x => x.LiquidityId).ToList();

            result.Add(liquidityInfoDto);
        }

        return result;
    }

    public async Task<List<LiquidityInfoDto>> GetMarketLiquidityListAsync(GetMyLiquidityListInput input)
    {
        var awakenLiquidityInfoList = await _farmProvider.GetAwakenLiquidityInfoAsync("ELF", "USDT");
        var myLiquidityList = await GetMyLiquidityListAsync(input);
        var rateDic = myLiquidityList.ToDictionary(x => x.Rate, x => x.LiquidityIds);
        var result = new List<LiquidityInfoDto>();
        foreach (var lpPriceItemDto in awakenLiquidityInfoList)
        {
            var liquidityInfoDto = _objectMapper.Map<LpPriceItemDto, LiquidityInfoDto>(lpPriceItemDto);
            liquidityInfoDto.Icons = new List<string>();
            if (rateDic.TryGetValue(liquidityInfoDto.Rate, out var ids))
            {
                liquidityInfoDto.LiquidityIds = ids;
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