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
using EcoEarnServer.Rewards.Provider;
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
    private const string TokenPoolUnLoginListRedisKeyPrefix = "EcoEarnServer:TokenPoolUnLoginList:";
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
    private readonly IRewardsProvider _rewardsProvider;
    private readonly PoolInfoOptions _poolInfoOptions;
    private readonly ProjectItemOptions _projectItemOptions;
    private readonly IMarketCapProvider _marketCapProvider;

    public TokenStakingService(ITokenStakingProvider tokenStakingProvider, IObjectMapper objectMapper,
        ILogger<TokenStakingService> logger, IOptions<RedisCacheOptions> optionsAccessor,
        IDistributedCacheSerializer serializer, IContractProvider contractProvider,
        IPriceProvider priceProvider,
        IOptionsSnapshot<TokenPoolIconsOptions> tokenPoolIconsOptions,
        IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions,
        IOptionsSnapshot<PoolTextWordOptions> poolTextWordOptions,
        IRewardsProvider rewardsProvider, IOptionsSnapshot<PoolInfoOptions> poolInfoOptions,
        IOptionsSnapshot<ProjectItemOptions> projectItemOptions,
        IMarketCapProvider marketCapProvider) : base(optionsAccessor)
    {
        _tokenStakingProvider = tokenStakingProvider;
        _objectMapper = objectMapper;
        _logger = logger;
        _serializer = serializer;
        _contractProvider = contractProvider;
        _priceProvider = priceProvider;
        _rewardsProvider = rewardsProvider;
        _marketCapProvider = marketCapProvider;
        _projectItemOptions = projectItemOptions.Value;
        _poolInfoOptions = poolInfoOptions.Value;
        _poolTextWordOptions = poolTextWordOptions.Value;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
        _tokenPoolIconsOptions = tokenPoolIconsOptions.Value;
    }

    public async Task<TokenPoolsResult> GetTokenPoolsAsync(GetTokenPoolsInput input)
    {
        var poolInfoDic = _poolInfoOptions.PoolInfoDic;
        if (string.IsNullOrEmpty(input.Address))
        {
            await ConnectAsync();
            var redisValue = await RedisDatabase.StringGetAsync(TokenPoolUnLoginListRedisKeyPrefix + input.PoolType);
            if (redisValue.HasValue)
            {
                return _serializer.Deserialize<TokenPoolsResult>(redisValue);
            }
        }

        var stakeCountList = await _tokenStakingProvider.GetStakeCountListAsync();
        var poolIdStakeCountDic = stakeCountList.GroupBy(x => x.PoolId)
            .ToDictionary(g => g.Key, g => g.First().Count);
        var tokenPoolsIndexerDtos = await _tokenStakingProvider.GetTokenPoolsAsync(input);
        var poolIds = tokenPoolsIndexerDtos.Select(x => x.PoolId).Distinct().ToList();
        var addressStakedInPoolDic = await _tokenStakingProvider.GetAddressStakedInPoolDicAsync(poolIds, input.Address);
        var dappIdDic = _projectItemOptions.ProjectItems.ToDictionary(x => x.DappId, x => x);
        var tokenPoolsList = new List<TokenPoolsDto>();
        foreach (var tokenPoolsIndexerDto in tokenPoolsIndexerDtos)
        {
            var currencyPair = $"{tokenPoolsIndexerDto.TokenPoolConfig.StakingToken.ToUpper()}_USDT";
            var usdtRate =
                await _priceProvider.GetGateIoPriceAsync(
                    $"{tokenPoolsIndexerDto.TokenPoolConfig.RewardToken.ToUpper()}_USDT");

            var feeRate = _lpPoolRateOptions.LpPoolRateDic.TryGetValue(
                tokenPoolsIndexerDto.TokenPoolConfig.StakeTokenContract,
                out var poolRate)
                ? poolRate
                : 0;
            var rate = tokenPoolsIndexerDto.PoolType == PoolTypeEnums.Token
                ? await _priceProvider.GetGateIoPriceAsync(currencyPair)
                : await _priceProvider.GetLpPriceAsync(tokenPoolsIndexerDto.TokenPoolConfig.StakingToken, feeRate);

            var tokenPoolsDto = _objectMapper.Map<TokenPoolsIndexerDto, TokenPoolsDto>(tokenPoolsIndexerDto);
            tokenPoolsDto.ProjectOwner =
                dappIdDic.TryGetValue(tokenPoolsIndexerDto.DappId, out var projectItem) ? projectItem.DappName : "";

            tokenPoolsDto.SupportEarlyStake = poolInfoDic.TryGetValue(tokenPoolsIndexerDto.PoolId, out var poolInfo) &&
                                              poolInfo.SupportEarlyStake;
            tokenPoolsDto.MinimalStakePeriod = poolInfo.MinimalStakePeriod;
            tokenPoolsDto.ExtendStakePeriod = poolInfo.ExtendStakePeriod;
            tokenPoolsDto.MinimalStakeAmount = poolInfo.MinimalStakeAmount;
            tokenPoolsDto.MinimalExtendStakeAmount = poolInfo.MinimalExtendStakeAmount;
            tokenPoolsDto.Sort = poolInfo?.Sort ?? 0;
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
            tokenPoolsDto.UsdRate = usdtRate;

            if (addressStakedInPoolDic.TryGetValue(tokenPoolsDto.PoolId, out var stakedInfos))
            {
                var rewardsListIndexerResult =
                    await _rewardsProvider.GetRewardsListAsync(input.PoolType, input.Address, 0, 1,
                        poolIds: new List<string> { tokenPoolsDto.PoolId });
                tokenPoolsDto.LatestClaimTime = !rewardsListIndexerResult.Data.IsNullOrEmpty()
                    ? rewardsListIndexerResult.Data.FirstOrDefault()!.ClaimedTime
                    : 0;
                tokenPoolsDto.StakeId = stakedInfos.StakeId;
                tokenPoolsDto.UnlockTime = stakedInfos.UnlockTime;
                tokenPoolsDto.StakingPeriod = stakedInfos.StakingPeriod;
                tokenPoolsDto.LastOperationTime = stakedInfos.LastOperationTime;
                var rewardDataDic = await GetStakedRewardsAsync(tokenPoolsDto.StakeId, input.ChainId);

                if (rewardDataDic.TryGetValue(stakedInfos.StakeId, out var rewardData) && rewardData.Amount != 0)
                {
                    tokenPoolsDto.Earned = rewardData.Amount.ToString();
                    tokenPoolsDto.EarnedInUsd =
                        (rewardData.Amount * usdtRate).ToString(CultureInfo.InvariantCulture);
                }

                var stakedAmount = stakedInfos.SubStakeInfos.Sum(x => x.StakedAmount + x.EarlyStakedAmount);
                var earlyStakedAmount = stakedInfos.SubStakeInfos
                    .Sum(x => x.EarlyStakedAmount);
                tokenPoolsDto.Staked = stakedAmount.ToString();
                tokenPoolsDto.EarlyStakedAmount = earlyStakedAmount;
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

            var symbolMarketCap =
                await _marketCapProvider.GetSymbolMarketCapAsync(tokenPoolsIndexerDto.TokenPoolConfig.StakingToken
                    .ToUpper());
            tokenPoolsDto.MarketCap = symbolMarketCap;
            
            tokenPoolsDto.Stakers =
                poolIdStakeCountDic.TryGetValue(tokenPoolsIndexerDto.PoolId, out var count) ? count : 0;
            tokenPoolsList.Add(tokenPoolsDto);
        }

        var result = new TokenPoolsResult()
        {
            Pools = tokenPoolsList.OrderBy(x => x.Sort).ToList(),
            TextNodes = JsonConvert.DeserializeObject<List<TextNodeDto>>(_poolTextWordOptions.PointsTextWord)
        };

        if (string.IsNullOrEmpty(input.Address))
        {
            await RedisDatabase.StringSetAsync(TokenPoolUnLoginListRedisKeyPrefix + input.PoolType,
                _serializer.Serialize(result),
                TimeSpan.FromHours(2));
        }

        return result;
    }

    public async Task<long> GetTokenPoolStakedSumAsync(GetTokenPoolStakedSumInput input)
    {
        var tokenPoolStakedInfoList =
            await _tokenStakingProvider.GetTokenPoolStakedInfoListAsync(new List<string> { input.PoolId });
        var tokenPoolStakedInfoDto = tokenPoolStakedInfoList.FirstOrDefault(x => x.PoolId == input.PoolId);
        return tokenPoolStakedInfoDto == null ? 0 : long.Parse(tokenPoolStakedInfoDto.TotalStakedAmount);
    }

    public async Task<List<EarlyStakeInfoDto>> GetStakedInfoAsync(GetEarlyStakeInfoInput input)
    {
        var result = new List<EarlyStakeInfoDto>();
        var tokenName = input.TokenName;
        var address = input.Address;
        var chainId = input.ChainId;
        var poolType = input.PoolType;
        var rate = input.Rate;
        var tokenPoolIndexerListDto = await _tokenStakingProvider.GetTokenPoolByTokenAsync(tokenName, poolType);

        if (!string.IsNullOrEmpty(tokenName) && poolType == PoolTypeEnums.Token)
        {
            tokenPoolIndexerListDto = new List<TokenPoolsIndexerDto> { tokenPoolIndexerListDto[0] };
        }

        if (!string.IsNullOrEmpty(tokenName) && poolType == PoolTypeEnums.Lp && rate != 0)
        {
            var stakeTokenContract =
                _lpPoolRateOptions.LpPoolRateDic.First(entity => Math.Abs(entity.Value - rate) == 0).Key;
            var tokenPoolsIndexerDto =
                tokenPoolIndexerListDto.First(x => x.TokenPoolConfig.StakeTokenContract == stakeTokenContract);
            tokenPoolIndexerListDto = new List<TokenPoolsIndexerDto> { tokenPoolsIndexerDto };
        }

        var poolIds = tokenPoolIndexerListDto.Select(x => x.PoolId).ToList();
        var stakedInfoIndexerList = await _tokenStakingProvider.GetStakedInfoAsync(tokenName, address, poolIds);
        var tokenStakedIndexerDtos = stakedInfoIndexerList.ToDictionary(x => x.PoolId, x => x);
        foreach (var tokenPoolIndexerDto in tokenPoolIndexerListDto)
        {
            var yearlyRewards = YearlyBlocks * tokenPoolIndexerDto.TokenPoolConfig.RewardPerBlock;
            var tokenPoolStakedSum = await GetTokenPoolStakedSumAsync(new GetTokenPoolStakedSumInput
                { PoolId = tokenPoolIndexerDto.PoolId, ChainId = chainId });
            var usdtRate =
                await _priceProvider.GetGateIoPriceAsync(
                    $"{tokenPoolIndexerDto.TokenPoolConfig.RewardToken.ToUpper()}_USDT");

            if (!tokenStakedIndexerDtos.TryGetValue(tokenPoolIndexerDto.PoolId, out var stakedInfoIndexerDtos))
            {
                stakedInfoIndexerDtos = new TokenStakedIndexerDto()
                {
                    StakingToken = tokenPoolIndexerDto.TokenPoolConfig.StakingToken
                };
            }

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
                Staked = stakedInfoIndexerDtos.LockState == LockState.Unlock
                    ? "0"
                    : stakedInfoIndexerDtos.SubStakeInfos.Sum(x => x.StakedAmount + x.EarlyStakedAmount).ToString(),
                YearlyRewards = yearlyRewards,
                FixedBoostFactor = tokenPoolIndexerDto.TokenPoolConfig.FixedBoostFactor,
                UnlockWindowDuration = tokenPoolIndexerDto.TokenPoolConfig.UnlockWindowDuration,
                MinimumClaimAmount = tokenPoolIndexerDto.TokenPoolConfig.MinimumClaimAmount,
                EarnedSymbol = tokenPoolIndexerDto.TokenPoolConfig.RewardToken,
                UsdRate = usdtRate,
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
            if (_poolInfoOptions.PoolInfoDic.TryGetValue(tokenPoolIndexerDto.PoolId, out var poolInfo))
            {
                stakeInfoDto.MinimalStakePeriod = poolInfo.MinimalStakePeriod;
                stakeInfoDto.ExtendStakePeriod = poolInfo.ExtendStakePeriod;
                stakeInfoDto.MinimalStakeAmount = poolInfo.MinimalStakeAmount;
                stakeInfoDto.MinimalExtendStakeAmount = poolInfo.MinimalExtendStakeAmount;
            }

            stakeInfoDto.StakeApr = stakeInfoDto.SubStakeInfos.Count == 0
                ? 0
                : stakeInfoDto.SubStakeInfos.Sum(x => x.Apr) / stakeInfoDto.SubStakeInfos.Count;

            result.Add(stakeInfoDto);
        }

        return result;
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
            var transactionResult =
                await _contractProvider.CallTransactionAsync<RewardDataListDto>(chainId, transaction);
            var rewardDataDic = transactionResult.RewardInfos.ToDictionary(x => x.StakeId, x => x);
            await RedisDatabase.StringSetAsync(TokenPoolStakedRewardsRedisKeyPrefix + stakeId,
                _serializer.Serialize(rewardDataDic), TimeSpan.FromSeconds(5));
            return rewardDataDic;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get staked rewards: fail. stakeId: {stakeId}", stakeId);
            return new Dictionary<string, RewardDataDto>();
        }
    }
}