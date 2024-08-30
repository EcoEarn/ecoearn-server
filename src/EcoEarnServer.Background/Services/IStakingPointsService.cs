using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Background.Services.Dtos;
using EcoEarnServer.Options;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Services;

public interface IStakingPointsService
{
    Task ExecuteAsync();
}

public class StakingPointsService : IStakingPointsService, ITransientDependency
{
    private readonly ITokenStakingProvider _tokenStakingProvider;
    private readonly IPriceProvider _priceProvider;
    private readonly LpPoolRateOptions _lpPoolRateOptions;

    public StakingPointsService(ITokenStakingProvider tokenStakingProvider, IPriceProvider priceProvider,
        IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions)
    {
        _tokenStakingProvider = tokenStakingProvider;
        _priceProvider = priceProvider;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
    }

    public async Task ExecuteAsync()
    {
        var tokenStakedIndexerDtos = await GetAllStakeInfoList();

        var allPools = await _tokenStakingProvider.GetTokenPoolsAsync(new GetTokenPoolsInput
        {
            PoolType = PoolTypeEnums.All
        });
        var poolIdDic = allPools.GroupBy(x => x.PoolId).ToDictionary(g => g.Key, g => g.First());
        var stakePriceDtoList = new List<StakePriceDto>();

        foreach (var tokenStakedIndexerDto in tokenStakedIndexerDtos)
        {
            var tokenStakeAmount = tokenStakedIndexerDto.SubStakeInfos
                .Sum(x => x.StakedAmount + x.EarlyStakedAmount) / 100000000;

            if (!poolIdDic.TryGetValue(tokenStakedIndexerDto.PoolId, out var poolInfo))
            {
                continue;
            }

            var stakingToken = poolInfo.TokenPoolConfig.StakingToken;
            var currencyPair = $"{stakingToken.ToUpper()}_USDT";

            double rate;
            if (poolInfo.PoolType == PoolTypeEnums.Token)
            {
                rate = await _priceProvider.GetGateIoPriceAsync(currencyPair);
            }
            else
            {
                var feeRate = _lpPoolRateOptions.LpPoolRateDic.TryGetValue(
                    poolInfo.TokenPoolConfig.StakeTokenContract,
                    out var poolRate)
                    ? poolRate
                    : 0;
                rate = await _priceProvider.GetLpPriceAsync(stakingToken, feeRate);
            }
        }

        throw new NotImplementedException();
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