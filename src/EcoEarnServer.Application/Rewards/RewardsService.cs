using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AElf.Types;
using EcoEarn.Contracts.Rewards;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.Grains.Grain.Rewards;
using EcoEarnServer.Options;
using EcoEarnServer.PointsStaking.Provider;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.Rewards.Provider;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Portkey.Contracts.CA;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Rewards;

public class RewardsService : IRewardsService, ISingletonDependency
{
    private const string LockKeyPrefix = "EcoEarnServer:RewardsWithdraw:Lock:";

    private readonly IRewardsProvider _rewardsProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<RewardsService> _logger;
    private readonly TokenPoolIconsOptions _tokenPoolIconsOptions;
    private readonly IPointsStakingProvider _pointsStakingProvider;
    private readonly ITokenStakingProvider _tokenStakingProvider;
    private readonly LpPoolRateOptions _lpPoolRateOptions;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IClusterClient _clusterClient;
    private readonly ChainOption _chainOption;
    private readonly ProjectKeyPairInfoOptions _projectKeyPairInfoOptions;
    private readonly ISecretProvider _secretProvider;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly EcoEarnContractOptions _earnContractOptions;
    private readonly ContractProvider _contractProvider;

    public RewardsService(IRewardsProvider rewardsProvider, IObjectMapper objectMapper, ILogger<RewardsService> logger,
        IOptionsSnapshot<TokenPoolIconsOptions> tokenPoolIconsOptions, IPointsStakingProvider pointsStakingProvider,
        ITokenStakingProvider tokenStakingProvider, IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions,
        IAbpDistributedLock distributedLock, IClusterClient clusterClient, IOptionsSnapshot<ChainOption> chainOption,
        IOptionsSnapshot<ProjectKeyPairInfoOptions> projectKeyPairInfoOptions, ISecretProvider secretProvider,
        IDistributedEventBus distributedEventBus, IOptionsSnapshot<EcoEarnContractOptions> earnContractOptions,
        ContractProvider contractProvider)
    {
        _rewardsProvider = rewardsProvider;
        _objectMapper = objectMapper;
        _logger = logger;
        _pointsStakingProvider = pointsStakingProvider;
        _tokenStakingProvider = tokenStakingProvider;
        _distributedLock = distributedLock;
        _clusterClient = clusterClient;
        _secretProvider = secretProvider;
        _distributedEventBus = distributedEventBus;
        _contractProvider = contractProvider;
        _earnContractOptions = earnContractOptions.Value;
        _projectKeyPairInfoOptions = projectKeyPairInfoOptions.Value;
        _chainOption = chainOption.Value;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
        _tokenPoolIconsOptions = tokenPoolIconsOptions.Value;
    }

    public async Task<PagedResultDto<RewardsListDto>> GetRewardsListAsync(GetRewardsListInput input)
    {
        var rewardsListIndexerResult = await _rewardsProvider.GetRewardsListAsync(input.PoolType, input.Address,
            input.SkipCount, input.MaxResultCount, filterUnlocked: input.FilterUnlocked);
        var result =
            _objectMapper.Map<List<RewardsListIndexerDto>, List<RewardsListDto>>(rewardsListIndexerResult.Data);

        var poolsIdDic = await GetPoolIdDicAsync(result);


        foreach (var rewardsListDto in result)
        {
            rewardsListDto.TokenIcon =
                _tokenPoolIconsOptions.TokenPoolIconsDic.TryGetValue(rewardsListDto.PoolId, out var icons)
                    ? icons
                    : rewardsListDto.PoolType == PoolTypeEnums.Points
                        ? new List<string> { }
                        : new List<string> { "" };

            if (!poolsIdDic.TryGetValue(rewardsListDto.PoolId, out var poolData))
            {
                continue;
            }

            rewardsListDto.Rate =
                _lpPoolRateOptions.LpPoolRateDic.TryGetValue(poolData.StakeTokenContract, out var poolRate)
                    ? poolRate
                    : 0;
            rewardsListDto.TokenName = poolData.PointsName;
        }


        return new PagedResultDto<RewardsListDto>
        {
            Items = result,
            TotalCount = rewardsListIndexerResult.TotalCount
        };
    }

    public async Task<RewardsAggregationDto> GetRewardsAggregationAsync(GetRewardsAggregationInput input)
    {
        var address = input.Address;
        var rewardsList = await GetAllRewardsList(address, PoolTypeEnums.All);
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
                        await GetRewardsAggAsync(keyValuePair.Value, address, 0);
                    break;
                case PoolTypeEnums.Token:
                    rewardsAggregationDto.TokenPoolAgg = await GetRewardsAggAsync(keyValuePair.Value, address, 0);
                    break;
                case PoolTypeEnums.Lp:
                    rewardsAggregationDto.LpPoolAgg = await GetRewardsAggAsync(keyValuePair.Value, address, 0);
                    break;
            }
        }

        if (string.IsNullOrEmpty(rewardsAggregationDto.PointsPoolAgg.RewardsTokenName))
        {
            var pointsPoolsIndexerDtos = await _pointsStakingProvider.GetPointsPoolsAsync("");
            var pointsPoolRewardsToken = pointsPoolsIndexerDtos.FirstOrDefault()?.PointsPoolConfig.RewardToken;
            rewardsAggregationDto.PointsPoolAgg.RewardsTokenName = pointsPoolRewardsToken;
        }

        if (string.IsNullOrEmpty(rewardsAggregationDto.TokenPoolAgg.RewardsTokenName))
        {
            var tokenPools = await _tokenStakingProvider.GetTokenPoolsAsync(new GetTokenPoolsInput()
            {
                PoolType = PoolTypeEnums.Token
            });
            var tokenPoolRewardsToken = tokenPools.FirstOrDefault()?.TokenPoolConfig.RewardToken;
            rewardsAggregationDto.TokenPoolAgg.RewardsTokenName = tokenPoolRewardsToken;
        }

        if (string.IsNullOrEmpty(rewardsAggregationDto.LpPoolAgg.RewardsTokenName))
        {
            var lpPools = await _tokenStakingProvider.GetTokenPoolsAsync(new GetTokenPoolsInput()
            {
                PoolType = PoolTypeEnums.Lp
            });
            var lpPoolRewardsToken = lpPools.FirstOrDefault()?.TokenPoolConfig.RewardToken;
            rewardsAggregationDto.LpPoolAgg.RewardsTokenName = lpPoolRewardsToken;
        }

        return rewardsAggregationDto;
    }

    public async Task<RewardsSignatureDto> RewardsWithdrawSignatureAsync(RewardsSignatureInput input)
    {
        return await RewardsSignatureAsync(input, ExecuteType.Withdrawn);
    }

    public async Task<string> RewardsWithdrawAsync(RewardsTransactionInput input)
    {
        var transaction =
            Transaction.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(input.RawTransaction));

        var withdrawInput = new WithdrawInput();
        if (transaction.To.ToBase58() == _earnContractOptions.CAContractAddress &&
            transaction.MethodName == "ManagerForwardCall")
        {
            var managerForwardCallInput = ManagerForwardCallInput.Parser.ParseFrom(transaction.Params);
            if (managerForwardCallInput.MethodName == "Withdraw" &&
                managerForwardCallInput.ContractAddress.ToBase58() ==
                _earnContractOptions.EcoEarnRewardsContractAddress)
            {
                withdrawInput = WithdrawInput.Parser.ParseFrom(managerForwardCallInput.Args);
            }
        }
        else if (transaction.To.ToBase58() == _earnContractOptions.EcoEarnRewardsContractAddress &&
                 transaction.MethodName == "Withdraw")
        {
            withdrawInput = WithdrawInput.Parser.ParseFrom(transaction.Params);
        }
        else
        {
            throw new UserFriendlyException("Invalid transaction");
        }

        var computedHash = HashHelper.ComputeFrom(new WithdrawInput
        {
            ClaimIds = { withdrawInput.ClaimIds },
            Account = withdrawInput.Account,
            Amount = withdrawInput.Amount,
            Seed = withdrawInput.Seed,
            ExpirationTime = withdrawInput.ExpirationTime,
            DappId = withdrawInput.DappId
        }.ToByteArray());
        if (!CryptoHelper.RecoverPublicKey(withdrawInput.Signature.ToByteArray(), computedHash.ToByteArray(),
                out var publicKeyByte))
        {
            throw new UserFriendlyException("invalid Signature");
        }

        var publicKey = publicKeyByte.ToHex();
        if (!_projectKeyPairInfoOptions.ProjectKeyPairInfos.TryGetValue(CommonConstant.Project, out var pubKey)
            || pubKey.PublicKey != publicKey)
        {
            throw new UserFriendlyException("invalid Signature");
        }

        if (withdrawInput.ExpirationTime * 1000 < DateTime.UtcNow.ToUtcMilliSeconds())
        {
            throw new UserFriendlyException("Please wait for the reward to be settled");
        }

        // var poolId = claimInput.PoolId.ToHex();
        // var address = claimInput.Account.ToBase58();
        // await SettleRewardsAsync(address, poolId, -((double)claimInput.Amount / 100000000));
        //
        // await UpdateClaimStatusAsync(address, poolId, "", DateTime.UtcNow.ToString("yyyyMMdd"));

        var transactionOutput = await _contractProvider.SendTransactionAsync(input.ChainId, transaction);
        return transactionOutput.TransactionId;
    }

    public async Task<RewardsSignatureDto> EarlyStakeSignatureAsync(RewardsSignatureInput input)
    {
        return await RewardsSignatureAsync(input, ExecuteType.Withdrawn);
    }

    public async Task<string> EarlyStakeAsync(RewardsTransactionInput input)
    {
        var transaction =
            Transaction.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(input.RawTransaction));

        var withdrawInput = new WithdrawInput();
        if (transaction.To.ToBase58() == _earnContractOptions.CAContractAddress &&
            transaction.MethodName == "ManagerForwardCall")
        {
            var managerForwardCallInput = ManagerForwardCallInput.Parser.ParseFrom(transaction.Params);
            if (managerForwardCallInput.MethodName == "Withdraw" &&
                managerForwardCallInput.ContractAddress.ToBase58() ==
                _earnContractOptions.EcoEarnRewardsContractAddress)
            {
                withdrawInput = WithdrawInput.Parser.ParseFrom(managerForwardCallInput.Args);
            }
        }
        else if (transaction.To.ToBase58() == _earnContractOptions.EcoEarnRewardsContractAddress &&
                 transaction.MethodName == "Withdraw")
        {
            withdrawInput = WithdrawInput.Parser.ParseFrom(transaction.Params);
        }
        else
        {
            throw new UserFriendlyException("Invalid transaction");
        }

        var computedHash = HashHelper.ComputeFrom(new WithdrawInput
        {
            ClaimIds = { withdrawInput.ClaimIds },
            Account = withdrawInput.Account,
            Amount = withdrawInput.Amount,
            Seed = withdrawInput.Seed,
            ExpirationTime = withdrawInput.ExpirationTime,
            DappId = withdrawInput.DappId
        }.ToByteArray());
        if (!CryptoHelper.RecoverPublicKey(withdrawInput.Signature.ToByteArray(), computedHash.ToByteArray(),
                out var publicKeyByte))
        {
            throw new UserFriendlyException("invalid Signature");
        }

        var publicKey = publicKeyByte.ToHex();
        if (!_projectKeyPairInfoOptions.ProjectKeyPairInfos.TryGetValue(CommonConstant.Project, out var pubKey)
            || pubKey.PublicKey != publicKey)
        {
            throw new UserFriendlyException("invalid Signature");
        }

        if (withdrawInput.ExpirationTime * 1000 < DateTime.UtcNow.ToUtcMilliSeconds())
        {
            throw new UserFriendlyException("Please wait for the reward to be settled");
        }

        // var poolId = claimInput.PoolId.ToHex();
        // var address = claimInput.Account.ToBase58();
        // await SettleRewardsAsync(address, poolId, -((double)claimInput.Amount / 100000000));
        //
        // await UpdateClaimStatusAsync(address, poolId, "", DateTime.UtcNow.ToString("yyyyMMdd"));

        var transactionOutput = await _contractProvider.SendTransactionAsync(input.ChainId, transaction);
        return transactionOutput.TransactionId;
    }


    private async Task<RewardsSignatureDto> RewardsSignatureAsync(RewardsSignatureInput input, ExecuteType executeType)
    {
        var poolType = input.PoolType;
        var address = input.Address;
        var amount = input.Amount;
        var executeClaimIds = input.ClaimInfos.Select(x => x.ClaimId).ToList();
        var dappId = input.DappId;
        var poolId = input.PoolId;
        var period = input.Period;

        //prevention of duplicate withdraw
        await using var handle = await _distributedLock.TryAcquireAsync(name: LockKeyPrefix + address);

        if (handle == null)
        {
            throw new UserFriendlyException("generating signature.");
        }

        var claimIdsHex = executeClaimIds.SelectMany(id => Encoding.UTF8.GetBytes(id)).ToArray().ToHex();
        var id = GuidHelper.GenerateId(address, poolType.ToString(), ExecuteType.Withdrawn.ToString(), claimIdsHex);
        var rewardOperationRecordGrain = _clusterClient.GetGrain<IRewardOperationRecordGrain>(id);
        var record = await rewardOperationRecordGrain.GetAsync();
        if (record != null)
        {
            _logger.LogWarning(
                "already generated signature. id: {id}", id);
            return new RewardsSignatureDto
            {
                Seed = HashHelper.ComputeFrom(record.Seed).ToHex(),
                Signature = ByteStringHelper.FromHexString(record.Signature),
                ExpirationTime = record.ExpiredTime / 1000
            };
        }

        //prevention of over withdraw
        var isValid = await CheckAmountValidityAsync(address, amount, executeClaimIds, poolType, executeType);
        if (!isValid)
        {
            throw new UserFriendlyException("invalid amount.");
        }

        // // //get withdrawing record
        // var withdrawingList = await _rewardsProvider.GetExecutingListAsync(address, ExecuteType.Withdrawn);
        // //check seeds is withdrawn
        // var seeds = withdrawingList.Select(x => x.Seed).ToList();
        // var realClaimInfoList = await _rewardsProvider.GetRealWithdrawnListAsync(seeds, address, poolId);
        // var realClaimSeeds = realClaimInfoList.Select(x => x.Seed).ToList();
        // foreach (var claimingRecord in claimingList.Where(
        //              claimingRecord => realClaimSeeds.Contains(claimingRecord.Seed)))
        // {
        //     //change record status
        //     await UpdateClaimStatusAsync(address, poolId, claimingRecord.Seed, "");
        //     //sub amount
        //     await SettleRewardsAsync(address, poolId, -((double)claimingRecord.Amount / 100000000));
        //     amount -= claimingRecord.Amount;
        // }

        var expiredPeriod =
            _projectKeyPairInfoOptions.ProjectKeyPairInfos.TryGetValue(CommonConstant.Project, out var projectInfo)
                ? projectInfo.ExpiredSeconds
                : 600;
        var now = DateTime.UtcNow;
        var expiredTime = new DateTime(now.Year, now.Month, now.Day)
            .AddDays(1)
            .AddSeconds(-expiredPeriod)
            .ToUtcMilliSeconds();
        var seed = Guid.NewGuid().ToString();
        IMessage data = executeType switch
        {
            ExecuteType.Withdrawn => new WithdrawInput
            {
                ClaimIds = { executeClaimIds.Select(Hash.LoadFromHex).ToList() },
                Account = Address.FromBase58(address),
                Amount = amount,
                Seed = HashHelper.ComputeFrom(seed),
                ExpirationTime = expiredTime / 1000,
                DappId = Hash.LoadFromHex(dappId)
            },
            ExecuteType.EarlyStake => new EarlyStakeInput
            {
                StakeInput = new StakeInput
                {
                    ClaimIds = { executeClaimIds.Select(Hash.LoadFromHex).ToList() },
                    Account = Address.FromBase58(address),
                    Amount = amount,
                    Seed = HashHelper.ComputeFrom(seed),
                    ExpirationTime = expiredTime / 1000,
                    PoolId = HashHelper.ComputeFrom(poolId),
                    Period = period,
                    DappId = Hash.LoadFromHex(dappId)
                }
            },
            ExecuteType.LiquidityAdded => new WithdrawInput
            {
                ClaimIds = { executeClaimIds.Select(Hash.LoadFromHex).ToList() },
                Account = Address.FromBase58(address),
                Amount = amount,
                Seed = HashHelper.ComputeFrom(seed),
                ExpirationTime = expiredTime / 1000,
                DappId = Hash.LoadFromHex(dappId)
            },
            _ => null
        };

        var signature = await GenerateSignatureByPubKeyAsync(projectInfo.PublicKey, data);

        //save signature
        var recordDto = new RewardOperationRecordDto()
        {
            Id = id,
            Amount = amount,
            Address = address,
            Seed = seed,
            Signature = signature,
            ClaimInfos = input.ClaimInfos,
            ExecuteStatus = ExecuteStatus.Executing,
            ExecuteType = executeType,
            CreateTime = DateTime.UtcNow.ToUtcMilliSeconds(),
            ExpiredTime = expiredTime
        };

        var saveResult = await rewardOperationRecordGrain.CreateAsync(recordDto);

        if (!saveResult.Success)
        {
            _logger.LogError(
                "save withdraw record fail, message:{message}, id: {id}", saveResult.Message, id);
            throw new UserFriendlyException(saveResult.Message);
        }

        await _distributedEventBus.PublishAsync(
            _objectMapper.Map<RewardOperationRecordDto, RewardOperationRecordEto>(saveResult.Data));

        return new RewardsSignatureDto
        {
            Seed = HashHelper.ComputeFrom(seed).ToHex(),
            Signature = ByteStringHelper.FromHexString(signature),
            ExpirationTime = expiredTime / 1000
        };
    }


    private async Task<Dictionary<string, PoolIdDataDto>> GetPoolIdDicAsync(List<RewardsListDto> result)
    {
        var pointsPoolIds = result
            .Where(x => x.PoolType == PoolTypeEnums.Points)
            .Select(x => x.PoolId)
            .ToList();
        var tokenPoolIds = result
            .Where(x => x.PoolType is PoolTypeEnums.Token or PoolTypeEnums.Lp)
            .Select(x => x.PoolId)
            .ToList();
        var pointsPoolsIndexerDtos = await _pointsStakingProvider.GetPointsPoolsAsync("", pointsPoolIds);
        var poolIdDic = pointsPoolsIndexerDtos.ToDictionary(x => x.PoolId, x => new PoolIdDataDto
        {
            PointsName = x.PointsName,
            DappId = x.DappId,
        });
        var input = new GetTokenPoolsInput()
        {
            PoolIds = tokenPoolIds,
            PoolType = PoolTypeEnums.All
        };
        var tokenPoolsIndexerDtos = await _tokenStakingProvider.GetTokenPoolsAsync(input);
        var tokenPoolIdDic = tokenPoolsIndexerDtos.ToDictionary(x => x.PoolId, x => new PoolIdDataDto
        {
            DappId = x.DappId,
            PointsName = x.TokenPoolConfig.StakingToken,
            StakeTokenContract = x.TokenPoolConfig.StakeTokenContract,
        });
        foreach (var poolIdDataDto in tokenPoolIdDic)
        {
            poolIdDic[poolIdDataDto.Key] = poolIdDataDto.Value;
        }

        return poolIdDic;
    }

    private async Task<string> GenerateSignatureByPubKeyAsync(string pubKey, IMessage param)
    {
        var dataHash = HashHelper.ComputeFrom(param);
        return await _secretProvider.GetSignatureFromHashAsync(pubKey, dataHash);
    }

    private async Task<bool> CheckAmountValidityAsync(string address, long amount, List<string> withdrawClaimIds,
        PoolTypeEnums poolType, ExecuteType executeType)
    {
        var rewardsAllList = await GetAllRewardsList(address, poolType);
        List<RewardsListIndexerDto> pastReleaseTimeClaimInfoList;
        if (executeType == ExecuteType.Withdrawn)
        {
            pastReleaseTimeClaimInfoList = rewardsAllList
                .Where(x => x.ReleaseTime < DateTime.UtcNow.ToUtcMilliSeconds())
                .ToList();
        }
        else
        {
            pastReleaseTimeClaimInfoList = rewardsAllList;
        }

        var pastReleaseTimeClaimIds = pastReleaseTimeClaimInfoList
            .Select(x => x.ClaimId)
            .Distinct()
            .ToList();

        var rewardOperationRecordList = await _rewardsProvider.GetRewardOperationRecordListAsync(address);
        //withdrawn
        var withdrawnClaimIds = rewardOperationRecordList
            .Where(x => x.ExecuteType == ExecuteType.Withdrawn)
            .SelectMany(x => x.ClaimInfos.Select(info => info.ClaimId))
            .ToList();

        //Early Staked
        var earlyStakedClaimIds = rewardOperationRecordList
            .Where(x => x.ExecuteType == ExecuteType.EarlyStake)
            .SelectMany(x => x.ClaimInfos.Select(info => info.ClaimId))
            .ToList();
        //Liquidity Added
        var liquidityAddedClaimIds = rewardOperationRecordList
            .Where(x => x.ExecuteType == ExecuteType.LiquidityAdded)
            .SelectMany(x => x.ClaimInfos.Select(info => info.ClaimId))
            .ToList();

        // Combine all excluded claim ids
        var excludedClaimIds = withdrawnClaimIds
            .Union(earlyStakedClaimIds)
            .Union(liquidityAddedClaimIds)
            .ToList();

        // Remove excluded claim ids from pastReleaseTimeClaimIds
        var resultList = pastReleaseTimeClaimIds
            .Except(excludedClaimIds)
            .ToList();

        var withdrawAmount = pastReleaseTimeClaimInfoList
            .Where(x => resultList.Contains(x.ClaimId))
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        return resultList.Count == withdrawClaimIds.Count && !resultList.Except(withdrawClaimIds).Any() &&
               amount.ToString() == withdrawAmount;
    }

    private async Task<RewardsAggDto> GetRewardsAggAsync(List<RewardsListIndexerDto> list, string address,
        double usdRate)
    {
        var pointsPoolAggDto = new RewardsAggDto();
        if (list.IsNullOrEmpty())
        {
            return pointsPoolAggDto;
        }

        pointsPoolAggDto.RewardsTokenName = list.FirstOrDefault()?.ClaimedSymbol;

        var totalRewards = list
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        pointsPoolAggDto.TotalRewards = totalRewards;
        pointsPoolAggDto.TotalRewardsInUsd =
            (double.Parse(totalRewards) * usdRate).ToString(CultureInfo.InvariantCulture);

        var withdrawn = list.Where(x => x.WithdrawTime != 0)
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        pointsPoolAggDto.Withdrawn = withdrawn;
        pointsPoolAggDto.WithdrawnInUsd = (double.Parse(withdrawn) * usdRate).ToString(CultureInfo.InvariantCulture);

        var unWithdrawList = list
            .Where(x => x.WithdrawTime != 0)
            .ToList();
        
        
        var rewardOperationRecordList = await _rewardsProvider.GetRewardOperationRecordListAsync(address);
        var rewardOperationRecordClaimIds = rewardOperationRecordList
            .SelectMany(x => x.ClaimInfos.Select(info => info.ClaimId).ToList())
            .ToList();
    
        var operationClaimList = unWithdrawList
            .Where(x => rewardOperationRecordClaimIds.Contains(x.ClaimId))
            .ToList();
        
        var earlyStakedIds = operationClaimList
            .Where(x => !string.IsNullOrEmpty(x.EarlyStakeSeed))
            .Select(x => x.StakeId)
            .Distinct().ToList();
            
        var unLockedStakeIds = await _rewardsProvider.GetUnLockedStakeIdsAsync(earlyStakedIds, address);
        
        var liquidityIds = operationClaimList
            .Where(x => !string.IsNullOrEmpty(x.LiquidityAddedSeed))
            .Select(x => x.LiquidityId)
            .ToList();
        var liquidityRemovedStakeIds = await _rewardsProvider.GetLiquidityRemovedLpIdsAsync(liquidityIds, address);
        
        var shouldRemoveClaimIds = operationClaimList
            .Where(x => !unLockedStakeIds.Contains(x.StakeId) && !liquidityRemovedStakeIds.Contains(x.StakeId))
            .Select(x => x.ClaimId)
            .ToList();

        var realList = unWithdrawList.Where(x => !shouldRemoveClaimIds.Contains(x.ClaimId))
            .ToList();

        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        var frozenList = realList.Where(x => x.ReleaseTime >= now)
            .OrderBy(x => x.ReleaseTime)
            .ToList();
        
        var frozen = frozenList
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        var withdrawableList = realList.Where(x => x.ReleaseTime < now)
            .ToList();
        var withdrawable = withdrawableList
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        pointsPoolAggDto.Frozen = frozen;
        pointsPoolAggDto.FrozenInUsd = (double.Parse(frozen) * usdRate).ToString(CultureInfo.InvariantCulture);
        pointsPoolAggDto.Withdrawable = withdrawable;
        pointsPoolAggDto.WithdrawableInUsd = (double.Parse(withdrawable) * usdRate).ToString(CultureInfo.InvariantCulture);
        
        var earlyStaked = operationClaimList
            .Where(x => earlyStakedIds.Contains(x.StakeId) && !unLockedStakeIds.Contains(x.StakeId))
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        pointsPoolAggDto.EarlyStakedAmount = earlyStaked;
        pointsPoolAggDto.EarlyStakedAmountInUsd = (double.Parse(earlyStaked) * usdRate).ToString(CultureInfo.InvariantCulture);
        
        pointsPoolAggDto.NextRewardsRelease = frozenList.First().ReleaseTime;
        pointsPoolAggDto.NextRewardsReleaseAmount = frozenList.First().ClaimedAmount;
        return pointsPoolAggDto;
    }

    private async Task<RewardsAggDto> GetTokenPoolRewardsAggAsync(List<RewardsListIndexerDto> list, string address)
    {
        var tokenPoolAggDto = new RewardsAggDto();
        if (list.IsNullOrEmpty())
        {
            return tokenPoolAggDto;
        }

        var stakeIds = list
            .Where(x => !string.IsNullOrEmpty(x.StakeId))
            .Select(x => x.StakeId)
            .Distinct().ToList();
        var unLockedStakeIds = await _rewardsProvider.GetUnLockedStakeIdsAsync(stakeIds, address);

        var stakingList = list
            .Where(x => x.EarlyStakeTime == 0 || unLockedStakeIds.Contains(x.StakeId))
            .ToList();

        tokenPoolAggDto.RewardsTokenName = stakingList.MaxBy(x => x.ClaimedTime)?.ClaimedSymbol;

        return tokenPoolAggDto;
    }

    private async Task<RewardsAggDto> GetLpPoolRewardsAggAsync(List<RewardsListIndexerDto> list, string address)
    {
        var tokenPoolAggDto = new RewardsAggDto();
        if (list.IsNullOrEmpty())
        {
            return tokenPoolAggDto;
        }

        var stakeIds = list
            .Where(x => !string.IsNullOrEmpty(x.StakeId))
            .Select(x => x.StakeId)
            .Distinct().ToList();
        var unLockedStakeIds = await _rewardsProvider.GetUnLockedStakeIdsAsync(stakeIds, address);

        var stakingList = list
            .Where(x => x.EarlyStakeTime == 0 || unLockedStakeIds.Contains(x.StakeId))
            .ToList();

        tokenPoolAggDto.RewardsTokenName = stakingList.MaxBy(x => x.ClaimedTime)?.ClaimedSymbol;
        return tokenPoolAggDto;
    }


    private async Task<List<RewardsListIndexerDto>> GetAllRewardsList(string address, PoolTypeEnums poolType)
    {
        var res = new List<RewardsListIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<RewardsListIndexerDto> list;
        do
        {
            var rewardsListIndexerResult = await _rewardsProvider.GetRewardsListAsync(poolType, address,
                skipCount, maxResultCount);
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