using System.Collections.Generic;
using System.Globalization;
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

        var result = new List<LiquidityInfoDto>();
        foreach (var dto in liquidityInfoIndexerDtos)
        {
            var liquidityInfoDtos =
                _objectMapper.Map<LiquidityInfoIndexerDto, LiquidityInfoDto>(dto);

            liquidityInfoDtos.Rate = _lpPoolRateOptions.LpPoolRateDic.TryGetValue(
                dto.TokenAddress,
                out var poolRate)
                ? poolRate
                : 0;

            var lpPrice = await _priceProvider.GetLpPriceAsync("ALP ELF-USD", liquidityInfoDtos.Rate);
            liquidityInfoDtos.Value =
                (double.Parse(liquidityInfoDtos.Banlance) * lpPrice).ToString(CultureInfo.InvariantCulture);
            liquidityInfoDtos.Icons = new List<string>();
            result.Add(liquidityInfoDtos);
        }

        return result;
    }

    public async Task<List<LiquidityInfoDto>> GetMarketLiquidityListAsync(GetMyLiquidityListInput input)
    {
        return await GetMyLiquidityListAsync(input);
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