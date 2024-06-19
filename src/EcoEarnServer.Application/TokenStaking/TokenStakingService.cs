using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
using EcoEarn.Contracts.Tokens;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.Options;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using Google.Protobuf.Collections;
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
            var feeRate = _lpPoolRateOptions.LpPoolRateDic.TryGetValue(
                tokenPoolsIndexerDto.TokenPoolConfig.StakeTokenContract,
                out var poolRate)
                ? poolRate
                : 0;
            var rate = tokenPoolsIndexerDto.PoolType == PoolTypeEnums.Token
                ? await _priceProvider.GetGateIoPriceAsync(currencyPair)
                : await _priceProvider.GetLpPriceAsync(tokenPoolsIndexerDto.TokenPoolConfig.StakingToken, feeRate);

            var tokenPoolsDto = _objectMapper.Map<TokenPoolsIndexerDto, TokenPoolsDto>(tokenPoolsIndexerDto);
            var tokenPoolStakedSumLong = await GetTokenPoolStakedSumAsync(new GetTokenPoolStakedSumInput
                { PoolId = tokenPoolsDto.PoolId, ChainId = input.ChainId });

            var tokenPoolStakedSum = (double)tokenPoolStakedSumLong;
            tokenPoolsDto.YearlyRewards = YearlyBlocks * tokenPoolsIndexerDto.TokenPoolConfig.RewardPerBlock;
            if (tokenPoolStakedSum != 0)
            {
                tokenPoolsDto.TotalStakeInUsd = (rate * tokenPoolStakedSum).ToString(CultureInfo.CurrentCulture);
                tokenPoolsDto.TotalStake = tokenPoolStakedSum.ToString(CultureInfo.InvariantCulture);
                tokenPoolsDto.AprMin = tokenPoolsDto.YearlyRewards / tokenPoolStakedSum;
                tokenPoolsDto.AprMax = tokenPoolsDto.AprMin *
                                       (1 + 360 / tokenPoolsIndexerDto.TokenPoolConfig.FixedBoostFactor);
            }

            tokenPoolsDto.Icons =
                _tokenPoolIconsOptions.TokenPoolIconsDic.TryGetValue(tokenPoolsDto.PoolId, out var icons)
                    ? icons
                    : new List<string>();
            tokenPoolsDto.Rate = feeRate;

            if (addressStakedInPoolDic.TryGetValue(tokenPoolsDto.PoolId, out var stakedInfos))
            {
                tokenPoolsDto.StakeId = stakedInfos.StakeId;
                tokenPoolsDto.UnlockTime = stakedInfos.UnlockTime;
                tokenPoolsDto.StakingPeriod = stakedInfos.StakingPeriod;
                tokenPoolsDto.LongestReleaseTime = stakedInfos.LongestReleaseTime;
                tokenPoolsDto.LastOperationTime = stakedInfos.LastOperationTime;
                var rewardDataDic = await GetStakedRewardsAsync(tokenPoolsDto.StakeId, input.ChainId);
                
                if (rewardDataDic.TryGetValue(stakedInfos.StakeId, out var rewardData) && rewardData.Amount != 0)
                {
                    var usdtRate = await _priceProvider.GetGateIoPriceAsync($"{rewardData.Symbol.ToUpper()}_USDT");
                    tokenPoolsDto.Earned = rewardData.Amount.ToString();
                    tokenPoolsDto.EarnedInUsd =
                        (rewardData.Amount * usdtRate).ToString(CultureInfo.InvariantCulture);
                }

                var stakedAmount = stakedInfos.SubStakeInfos.Sum(x => x.StakedAmount);
                tokenPoolsDto.Staked = stakedAmount.ToString();
                tokenPoolsDto.StakedInUsd = stakedInfos.LockState == LockState.Unlock
                    ? "0"
                    : (rate * stakedAmount).ToString(CultureInfo.CurrentCulture);
                var stakeInfoDtos = new List<SubStakeInfoDto>();

                foreach (var subsStakedInfo in stakedInfos.SubStakeInfos)
                {
                    var subStakeInfoDto = _objectMapper.Map<SubStakeInfoIndexerDto, SubStakeInfoDto>(subsStakedInfo);
                    subStakeInfoDto.Apr = tokenPoolsDto.AprMin *
                                          (1 + (double)subsStakedInfo.Period / 86400 / tokenPoolsDto.FixedBoostFactor);
                    stakeInfoDtos.Add(subStakeInfoDto);
                }

                tokenPoolsDto.StakeInfos = stakeInfoDtos;
            }

            tokenPoolsDto.StakeApr = tokenPoolsDto.StakeInfos.Count == 0
                ? 0
                : tokenPoolsDto.StakeInfos.Sum(x => x.Apr) / tokenPoolsDto.StakeInfos.Count;
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
            StakeSymbol = string.IsNullOrEmpty(stakedInfoIndexerDtos.StakingToken)
                ? tokenName
                : stakedInfoIndexerDtos.StakingToken,
            UnlockTime = stakedInfoIndexerDtos.UnlockTime,
            LastOperationTime = stakedInfoIndexerDtos.LastOperationTime,
            StakingPeriod = stakedInfoIndexerDtos.StakingPeriod,
            LongestReleaseTime = stakedInfoIndexerDtos.LongestReleaseTime,
            Staked = stakedInfoIndexerDtos.LockState == LockState.Unlock
                ? "0"
                : stakedInfoIndexerDtos.SubStakeInfos.Sum(x => x.StakedAmount).ToString(),
            YearlyRewards = yearlyRewards,
            FixedBoostFactor = tokenPoolIndexerDto.TokenPoolConfig.FixedBoostFactor,
            UnlockWindowDuration = tokenPoolIndexerDto.TokenPoolConfig.UnlockWindowDuration,
            MinimumClaimAmount = tokenPoolIndexerDto.TokenPoolConfig.MinimumClaimAmount,
            SubStakeInfos = stakedInfoIndexerDtos.SubStakeInfos.Select(dto =>
            {
                var subStakeInfoDto = _objectMapper.Map<SubStakeInfoIndexerDto, SubStakeInfoDto>(dto);
                subStakeInfoDto.Apr = tokenPoolStakedSum == 0
                    ? 0
                    : (double)yearlyRewards / tokenPoolStakedSum * (1 + (double)dto.Period / 86400 /
                        tokenPoolIndexerDto.TokenPoolConfig.FixedBoostFactor);
                return subStakeInfoDto;
            }).ToList(),
        };
        stakeInfoDto.StakeApr = stakeInfoDto.SubStakeInfos.Count == 0
            ? 0
            : stakeInfoDto.SubStakeInfos.Sum(x => x.Apr) / stakeInfoDto.SubStakeInfos.Count;
        return stakeInfoDto;
    }


    private async Task<Dictionary<string, RewardDataDto>> GetStakedRewardsAsync(string stakeId, string chainId)
    {
        try
        {
            await ConnectAsync();
            var redisValue = await RedisDatabase.StringGetAsync(TokenPoolStakedRewardsRedisKeyPrefix + stakeId);
            if (redisValue.HasValue)
            {
                _logger.LogInformation("get staked rewards: {rewards}", redisValue);
                return _serializer.Deserialize<Dictionary<string, RewardDataDto>>(redisValue);
            }
            var repeatedField = new RepeatedField<Hash>();
            repeatedField.Add(Hash.LoadFromHex(stakeId));
            var input = new GetRewardInput()
            {
                StakeIds = { repeatedField }
            };

            var transaction = _contractProvider
                .CreateTransaction(chainId, ContractConstants.SenderName, ContractConstants.ContractName,
                    ContractConstants.StakedRewardsMethodName, input)
                .Result
                .transaction;
            var transactionResult = await _contractProvider.CallTransactionAsync<RewardDataListDto>(chainId, transaction);
            var rewardDataDic = transactionResult.RewardInfos.ToDictionary(x => x.StakeId, x => x);
            await RedisDatabase.StringSetAsync(TokenPoolStakedRewardsRedisKeyPrefix + stakeId,
                _serializer.Serialize(rewardDataDic), TimeSpan.FromSeconds(5));
            return rewardDataDic;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get staked rewards: fail. stakeId: {stakeId}", stakeId);
            return new System.Collections.Generic.Dictionary<string, RewardDataDto>();
        }
    }
}