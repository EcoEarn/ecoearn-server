using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
using EcoEarn.Contracts.Tokens;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.TokenStaking;

public class TokenStakingService : AbpRedisCache, ITokenStakingService, ISingletonDependency
{
    private const string TokenPoolStakedSumRedisKeyPrefix = "EcoEarnServer:TokenPoolStakedSum:";
    private const long YearlyBlocks = 172800 * 360;

    private readonly ITokenStakingProvider _tokenStakingProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TokenStakingService> _logger;
    private readonly IDistributedCacheSerializer _serializer;
    private readonly IContractProvider _contractProvider;
    private readonly IPriceProvider _priceProvider;

    public TokenStakingService(ITokenStakingProvider tokenStakingProvider, IObjectMapper objectMapper,
        ILogger<TokenStakingService> logger, IOptions<RedisCacheOptions> optionsAccessor,
        IDistributedCacheSerializer serializer, IContractProvider contractProvider,
        IPriceProvider priceProvider) : base(optionsAccessor)
    {
        _tokenStakingProvider = tokenStakingProvider;
        _objectMapper = objectMapper;
        _logger = logger;
        _serializer = serializer;
        _contractProvider = contractProvider;
        _priceProvider = priceProvider;
    }

    public async Task<List<TokenPoolsDto>> GetTokenPoolsAsync(GetTokenPoolsInput input)
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
            var tokenPoolStakedSum = await GetTokenPoolStakedSumAsync(new GetTokenPoolStakedSumInput
                { PoolId = tokenPoolsDto.PoolId, ChainId = input.ChainId});
            if (tokenPoolStakedSum != 0)
            {
                tokenPoolsDto.TotalStakeInUsd = (rate * tokenPoolStakedSum).ToString(CultureInfo.CurrentCulture);
            }

            tokenPoolsDto.TotalStake = tokenPoolStakedSum.ToString();
            tokenPoolsDto.YearlyRewards = YearlyBlocks * tokenPoolsIndexerDto.TokenPoolConfig.RewardPerBlock;
            tokenPoolsDto.AprMin = (double)tokenPoolsDto.YearlyRewards / tokenPoolStakedSum * 100;
            tokenPoolsDto.AprMax = tokenPoolsDto.AprMin * 2;

            if (addressStakedInPoolDic.TryGetValue(tokenPoolsDto.PoolId, out var stakedInfo))
            {
                tokenPoolsDto.StakeId = stakedInfo.StakeId;
                tokenPoolsDto.Earned = stakedInfo.StakeId;
                tokenPoolsDto.EarnedInUsd = stakedInfo.StakeId;
                tokenPoolsDto.Staked = (stakedInfo.StakedAmount + stakedInfo.EarlyStakedAmount).ToString();
                tokenPoolsDto.StakedInUsd =
                    (rate * (stakedInfo.StakedAmount + stakedInfo.EarlyStakedAmount)).ToString(CultureInfo
                        .CurrentCulture);
                tokenPoolsDto.StakedAmount = stakedInfo.StakedAmount.ToString();
                tokenPoolsDto.EarlyStakedAmount = stakedInfo.EarlyStakedAmount.ToString();
                tokenPoolsDto.UnlockTime = stakedInfo.StakedTime + stakedInfo.Period * 86400000;
                tokenPoolsDto.StakeApr = tokenPoolsDto.AprMin * (1 + (double)stakedInfo.Period / 360);
                tokenPoolsDto.StakedTime = stakedInfo.StakedTime;
                tokenPoolsDto.Period = stakedInfo.Period;
            }

            tokenPoolsList.Add(tokenPoolsDto);
        }

        return tokenPoolsList;
    }

    public async Task<long> GetTokenPoolStakedSumAsync(GetTokenPoolStakedSumInput input)
    {
        try
        {
            await ConnectAsync();
            var redisValue = await RedisDatabase.StringGetAsync(TokenPoolStakedSumRedisKeyPrefix + input.PoolId);
            if (redisValue.HasValue)
            {
                _logger.LogInformation("get token pool stated sum: {sum}", redisValue);
                return _serializer.Deserialize<long>(redisValue);
            }

            var transaction = _contractProvider
                .CreateTransaction(input.ChainId, ContractConstants.SenderName, ContractConstants.ContractName,
                    ContractConstants.StakedSumMethodName, Hash.LoadFromHex(input.PoolId))
                .Result
                .transaction;
            var totalStakedAmount =
                _contractProvider.CallTransactionAsync<PoolDataDto>(input.ChainId, transaction).Result.TotalStakedAmount;
            await RedisDatabase.StringSetAsync(TokenPoolStakedSumRedisKeyPrefix + input.PoolId, totalStakedAmount);
            return long.Parse(totalStakedAmount);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get token pool staked sum fail. poolId: {poolId}", input.PoolId);
            throw new UserFriendlyException("get token pool staked sum fail.");
        }
    }

    public async Task<EarlyStakeInfoDto> GetStakedInfoAsync(string tokenName)
    {
        var stakedInfoIndexerDtos = await _tokenStakingProvider.GetStakedInfoAsync(tokenName);
        var tokenPoolIndexerDto = await _tokenStakingProvider.GetTokenPoolByTokenAsync(tokenName);
        var yearlyRewards = YearlyBlocks * tokenPoolIndexerDto.TokenPoolConfig.RewardPerBlock;
        var tokenPoolStakedSum = await GetTokenPoolStakedSumAsync(new GetTokenPoolStakedSumInput
            { PoolId = tokenPoolIndexerDto.PoolId });
        var stakeInfoDto = new EarlyStakeInfoDto
        {
            StakeId = stakedInfoIndexerDtos.StakeId,
            Staked = stakedInfoIndexerDtos.StakedAmount.ToString(),
            StakeSymbol = stakedInfoIndexerDtos.StakingToken,
            StakedTime = stakedInfoIndexerDtos.StakedTime,
            UnlockTime = stakedInfoIndexerDtos.StakedTime + stakedInfoIndexerDtos.Period * 86400000,
            StakeApr = (double)yearlyRewards / tokenPoolStakedSum * 100 *
                       (1 + (double)stakedInfoIndexerDtos.Period / 360),
            Period = stakedInfoIndexerDtos.Period,
            YearlyRewards = yearlyRewards
        };
        return stakeInfoDto;
    }
}