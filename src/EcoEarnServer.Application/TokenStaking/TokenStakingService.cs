using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.Options;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.TokenStaking;

public class TokenStakingService : AbpRedisCache, ITokenStakingService, ISingletonDependency
{
    private const string TokenPoolStakedSumRedisKeyPrefix = "EcoEarnServer:TokenPoolStakedSum:";
    private const string TokenPoolStakedRewardsRedisKeyPrefix = "EcoEarnServer:TokenPoolStakedRewards:";
    private const long YearlyBlocks = 86400 * 360;

    private readonly ITokenStakingProvider _tokenStakingProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TokenStakingService> _logger;
    private readonly IDistributedCacheSerializer _serializer;
    private readonly IContractProvider _contractProvider;
    private readonly IPriceProvider _priceProvider;
    private readonly TokenPoolIconsOptions _tokenPoolIconsOptions;
    private readonly LpPoolRateOptions _lpPoolRateOptions;
    private readonly PoolTextWordOptions _poolTextWordOptions;

    public TokenStakingService(ITokenStakingProvider tokenStakingProvider, IObjectMapper objectMapper,
        ILogger<TokenStakingService> logger, IOptions<RedisCacheOptions> optionsAccessor,
        IDistributedCacheSerializer serializer, IContractProvider contractProvider,
        IPriceProvider priceProvider,
        IOptionsSnapshot<TokenPoolIconsOptions> tokenPoolIconsOptions,
        IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions,
        IOptionsSnapshot<PoolTextWordOptions> poolTextWordOptions) : base(optionsAccessor)
    {
        _tokenStakingProvider = tokenStakingProvider;
        _objectMapper = objectMapper;
        _logger = logger;
        _serializer = serializer;
        _contractProvider = contractProvider;
        _priceProvider = priceProvider;
        _poolTextWordOptions = poolTextWordOptions.Value;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
        _tokenPoolIconsOptions = tokenPoolIconsOptions.Value;
    }

    public async Task<TokenPoolsResult> GetTokenPoolsAsync(GetTokenPoolsInput input)
    {
        var tokenPoolsIndexerDtos = await _tokenStakingProvider.GetTokenPoolsAsync(input);
        var poolIds = tokenPoolsIndexerDtos.Select(x => x.PoolId).Distinct().ToList();
        var addressStakedInPoolDic = await _tokenStakingProvider.GetAddressStakedInPoolDicAsync(poolIds, input.Address);

        var tokenPoolsList = new List<TokenPoolsDto>();
        foreach (var tokenPoolsIndexerDto in tokenPoolsIndexerDtos)
        {
            var currencyPair = $"{tokenPoolsIndexerDto.TokenPoolConfig.StakingToken.ToUpper()}_USDT";
            var rate = await _priceProvider.GetGateIoPriceAsync(currencyPair);

            var tokenPoolsDto = _objectMapper.Map<TokenPoolsIndexerDto, TokenPoolsDto>(tokenPoolsIndexerDto);
            var tokenPoolStakedSumLong = await GetTokenPoolStakedSumAsync(new GetTokenPoolStakedSumInput
                { PoolId = tokenPoolsDto.PoolId, ChainId = input.ChainId });

            var tokenPoolStakedSum = (double)tokenPoolStakedSumLong;
            tokenPoolsDto.YearlyRewards = YearlyBlocks * tokenPoolsIndexerDto.TokenPoolConfig.RewardPerBlock;
            if (tokenPoolStakedSum != 0)
            {
                tokenPoolsDto.TotalStakeInUsd = (rate * tokenPoolStakedSum).ToString(CultureInfo.CurrentCulture);
                tokenPoolsDto.TotalStake = tokenPoolStakedSum.ToString(CultureInfo.InvariantCulture);
                tokenPoolsDto.AprMin = (double)tokenPoolsDto.YearlyRewards / tokenPoolStakedSum;
                tokenPoolsDto.AprMax = tokenPoolsDto.AprMin *
                                       (1 + 360 / tokenPoolsIndexerDto.TokenPoolConfig.FixedBoostFactor);
            }

            tokenPoolsDto.Icons =
                _tokenPoolIconsOptions.TokenPoolIconsDic.TryGetValue(tokenPoolsDto.PoolId, out var icons)
                    ? icons
                    : new List<string>();
            tokenPoolsDto.Rate =
                _lpPoolRateOptions.LpPoolRateDic.TryGetValue(tokenPoolsIndexerDto.TokenPoolConfig.StakeTokenContract,
                    out var poolRate)
                    ? poolRate
                    : 0;


            if (addressStakedInPoolDic.TryGetValue(tokenPoolsDto.PoolId, out var stakedInfo))
            {
                var rewardData = await GetStakedRewardsAsync(stakedInfo.StakeId, input.ChainId);
                if (rewardData.Amount != 0)
                {
                    //var usdtRate = await _priceProvider.GetGateIoPriceAsync($"{rewardData.Symbol.ToUpper()}_USDT");
                    tokenPoolsDto.Earned = rewardData.Amount.ToString();
                    //tokenPoolsDto.EarnedInUsd = rewardData.Amount * usdtRate ;
                }

                tokenPoolsDto.StakeId = stakedInfo.StakeId;
                tokenPoolsDto.StakingPeriod = stakedInfo.StakingPeriod;
                tokenPoolsDto.BoostedAmount = stakedInfo.BoostedAmount;
                tokenPoolsDto.LastOperationTime = stakedInfo.LastOperationTime;
                tokenPoolsDto.Staked = stakedInfo.LockState == LockState.Unlock
                    ? "0"
                    : (stakedInfo.StakedAmount + stakedInfo.EarlyStakedAmount).ToString();
                tokenPoolsDto.StakedInUsd = stakedInfo.LockState == LockState.Unlock
                    ? "0"
                    : (rate * (stakedInfo.StakedAmount + stakedInfo.EarlyStakedAmount)).ToString(CultureInfo
                        .CurrentCulture);
                tokenPoolsDto.StakedAmount = stakedInfo.StakedAmount.ToString();
                tokenPoolsDto.EarlyStakedAmount = stakedInfo.EarlyStakedAmount.ToString();
                tokenPoolsDto.UnlockTime = stakedInfo.StakedTime + stakedInfo.Period * 1000;
                tokenPoolsDto.StakeApr = tokenPoolsDto.AprMin *
                                         (1 + (double)stakedInfo.Period / 86400 / tokenPoolsDto.FixedBoostFactor);
                tokenPoolsDto.StakedTime = stakedInfo.StakedTime;
                tokenPoolsDto.Period = stakedInfo.Period;
            }

            tokenPoolsList.Add(tokenPoolsDto);
        }

        return new TokenPoolsResult()
        {
            Pools = tokenPoolsList,
            TextNodes = JsonConvert.DeserializeObject<List<TextNodeDto>>(_poolTextWordOptions.PointsTextWord)
        };
    }

    public async Task<long> GetTokenPoolStakedSumAsync(GetTokenPoolStakedSumInput input)
    {
        var tokenPoolStakedInfoList =
            await _tokenStakingProvider.GetTokenPoolStakedInfoListAsync(new List<string> { input.PoolId });
        var tokenPoolStakedInfoDto = tokenPoolStakedInfoList.FirstOrDefault(x => x.PoolId == input.PoolId);
        return tokenPoolStakedInfoDto == null ? 0 : long.Parse(tokenPoolStakedInfoDto.TotalStakedAmount);
    }

    public async Task<EarlyStakeInfoDto> GetStakedInfoAsync(string tokenName, string address, string chainId)
    {
        var stakedInfoIndexerDtos = await _tokenStakingProvider.GetStakedInfoAsync(tokenName, address);
        var tokenPoolIndexerDto = await _tokenStakingProvider.GetTokenPoolByTokenAsync(tokenName);
        var yearlyRewards = YearlyBlocks * tokenPoolIndexerDto.TokenPoolConfig.RewardPerBlock;
        var tokenPoolStakedSum = await GetTokenPoolStakedSumAsync(new GetTokenPoolStakedSumInput
            { PoolId = tokenPoolIndexerDto.PoolId, ChainId = chainId });
        var stakeInfoDto = new EarlyStakeInfoDto
        {
            StakeId = stakedInfoIndexerDtos.StakeId,
            PoolId = tokenPoolIndexerDto.PoolId,
            StakingPeriod = stakedInfoIndexerDtos.StakingPeriod,
            Staked = stakedInfoIndexerDtos.LockState == LockState.Unlock
                ? "0"
                : (stakedInfoIndexerDtos.StakedAmount + stakedInfoIndexerDtos.EarlyStakedAmount).ToString(),
            StakedAmount = stakedInfoIndexerDtos.StakedAmount.ToString(),
            StakeSymbol = stakedInfoIndexerDtos.StakingToken,
            StakedTime = stakedInfoIndexerDtos.StakedTime,
            UnlockTime = stakedInfoIndexerDtos.StakedTime + stakedInfoIndexerDtos.Period * 1000,

            StakeApr = tokenPoolStakedSum == 0
                ? 0
                : (double)yearlyRewards / tokenPoolStakedSum * (1 + (double)stakedInfoIndexerDtos.Period / 86400 /
                    tokenPoolIndexerDto.TokenPoolConfig.FixedBoostFactor),
            Period = stakedInfoIndexerDtos.Period,
            YearlyRewards = yearlyRewards,
            FixedBoostFactor = tokenPoolIndexerDto.TokenPoolConfig.FixedBoostFactor,
            BoostedAmount = stakedInfoIndexerDtos.BoostedAmount,
            UnlockWindowDuration = tokenPoolIndexerDto.TokenPoolConfig.UnlockWindowDuration,
            LastOperationTime = stakedInfoIndexerDtos.LastOperationTime,
            EarlyStakedAmount = stakedInfoIndexerDtos.EarlyStakedAmount.ToString(),
            MinimumClaimAmount = tokenPoolIndexerDto.TokenPoolConfig.MinimumClaimAmount,
        };
        return stakeInfoDto;
    }


    private async Task<RewardDataDto> GetStakedRewardsAsync(string stakeId, string chainId)
    {
        try
        {
            await ConnectAsync();
            var redisValue = await RedisDatabase.StringGetAsync(TokenPoolStakedRewardsRedisKeyPrefix + stakeId);
            if (redisValue.HasValue)
            {
                _logger.LogInformation("get staked rewards: {rewards}", redisValue);
                return _serializer.Deserialize<RewardDataDto>(redisValue);
            }

            var transaction = _contractProvider
                .CreateTransaction(chainId, ContractConstants.SenderName, ContractConstants.ContractName,
                    ContractConstants.StakedRewardsMethodName, Hash.LoadFromHex(stakeId))
                .Result
                .transaction;
            var transactionResult = await _contractProvider.CallTransactionAsync<RewardDataDto>(chainId, transaction);
            await RedisDatabase.StringSetAsync(TokenPoolStakedRewardsRedisKeyPrefix + stakeId,
                _serializer.Serialize(transactionResult), TimeSpan.FromSeconds(5));
            return transactionResult;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get staked rewards: fail. stakeId: {stakeId}", stakeId);
            return new RewardDataDto();
        }
    }
}