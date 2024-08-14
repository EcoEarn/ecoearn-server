using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AElf.Types;
using EcoEarn.Contracts.Points;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.Grains.Grain.PointsPool;
using EcoEarnServer.Grains.Grain.PointsStakeRewards;
using EcoEarnServer.Options;
using EcoEarnServer.PointsPool;
using EcoEarnServer.PointsStakeRewards;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.PointsStaking.Provider;
using EcoEarnServer.TokenStaking;
using Google.Protobuf;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using Portkey.Contracts.CA;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.PointsStaking;

public class PointsStakingService : AbpRedisCache, IPointsStakingService, ISingletonDependency
{
    private const string LockKeyPrefix = "EcoEarnServer:PointsRewardsClaim:Lock:";
    private const string PointsPoolNowSnapshotRedisKeyPrefix = "EcoEarnServer:PointsPoolNowSnapshot:";
    private const string PointsPoolStakeAmountRedisKeyPrefix = "EcoEarnServer:PointsPoolStakeAmount:";
    private const string PointsPoolStakeRewardsRedisKeyPrefix = "EcoEarnServer:PointsPoolStakeRewards:";
    private const string PointsPoolUnLoginListRedisKeyPrefix = "EcoEarnServer:PointsPoolUnLoginList:";


    private readonly ProjectItemOptions _projectItemOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IPointsStakingProvider _pointsStakingProvider;
    private readonly EcoEarnContractOptions _earnContractOptions;
    private readonly ContractProvider _contractProvider;
    private readonly ProjectKeyPairInfoOptions _projectKeyPairInfoOptions;
    private readonly IClusterClient _clusterClient;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILogger<PointsStakingService> _logger;
    private readonly ITokenStakingService _tokenStakingService;
    private readonly ChainOption _chainOption;
    private readonly ISecretProvider _secretProvider;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly PoolTextWordOptions _poolTextWordOptions;
    private readonly IDistributedCacheSerializer _serializer;

    public PointsStakingService(IOptionsSnapshot<ProjectItemOptions> projectItemOptions, IObjectMapper objectMapper,
        IPointsStakingProvider pointsStakingProvider, IOptionsSnapshot<EcoEarnContractOptions> earnContractOptions,
        ContractProvider contractProvider, IOptionsSnapshot<ProjectKeyPairInfoOptions> projectKeyPairInfoOptions,
        IClusterClient clusterClient, IDistributedEventBus distributedEventBus, ILogger<PointsStakingService> logger,
        ITokenStakingService tokenStakingService, IOptionsSnapshot<ChainOption> chainOption,
        ISecretProvider secretProvider, IAbpDistributedLock distributedLock,
        IOptionsSnapshot<PoolTextWordOptions> poolTextWordOptions,
        IOptions<RedisCacheOptions> optionsAccessor, IDistributedCacheSerializer serializer) : base(optionsAccessor)
    {
        _objectMapper = objectMapper;
        _pointsStakingProvider = pointsStakingProvider;
        _contractProvider = contractProvider;
        _clusterClient = clusterClient;
        _distributedEventBus = distributedEventBus;
        _logger = logger;
        _tokenStakingService = tokenStakingService;
        _secretProvider = secretProvider;
        _distributedLock = distributedLock;
        _serializer = serializer;
        _poolTextWordOptions = poolTextWordOptions.Value;
        _chainOption = chainOption.Value;
        _projectKeyPairInfoOptions = projectKeyPairInfoOptions.Value;
        _earnContractOptions = earnContractOptions.Value;
        _projectItemOptions = projectItemOptions.Value;
    }

    public async Task<List<ProjectItemListDto>> GetProjectItemListAsync()
    {
        var projectItemListDtos =
            _objectMapper.Map<List<ProjectItem>, List<ProjectItemListDto>>(_projectItemOptions.ProjectItems);

        var projectItemAggDataDic = await GetProjectItemAggDataDic();
        projectItemListDtos.ForEach(dto =>
        {
            if (!projectItemAggDataDic.TryGetValue(dto.DappId, out var aggData))
            {
                return;
            }

            dto.Tvl = aggData.Tvl;
            dto.StakingAddress = aggData.StakingAddress;
        });
        return projectItemListDtos;
    }

    public async Task<List<PointsPoolsDto>> GetPointsPoolsAsync(GetPointsPoolsInput input)
    {
        var dappId = input.DappId;
        if (string.IsNullOrEmpty(input.Address))
        {
            await ConnectAsync();
            var redisValue =
                await RedisDatabase.StringGetAsync(PointsPoolUnLoginListRedisKeyPrefix + input.Type + ":" + dappId);
            if (redisValue.HasValue)
            {
                return _serializer.Deserialize<List<PointsPoolsDto>>(redisValue);
            }
        }

        var pointsPoolsIndexerList = await _pointsStakingProvider.GetPointsPoolsAsync(input.Name, dappId);
        var poolIds = pointsPoolsIndexerList.Select(pool => pool.PoolId).ToList();
        var pointsPoolsDtos =
            _objectMapper.Map<List<PointsPoolsIndexerDto>, List<PointsPoolsDto>>(pointsPoolsIndexerList);
        var pointsPoolStakeSumDic = await _pointsStakingProvider.GetPointsPoolStakeSumDicAsync(poolIds);
        var addressStakeAmountDic = await GetAddressStakeAmountDicAsync(input.Address, dappId);
        var addressStakeRewardsDic = await GetAddressStakeRewardsDicAsync(input.Address, dappId);
        pointsPoolsDtos.ForEach(dto =>
        {
            dto.TotalStake = pointsPoolStakeSumDic.TryGetValue(dto.PoolId, out var totalStake) ? totalStake : "0";
            dto.Staked =
                addressStakeAmountDic.TryGetValue(GuidHelper.GenerateId(input.Address, dto.PoolId), out var staked)
                    ? staked
                    : "0";
            dto.Earned =
                addressStakeRewardsDic.TryGetValue(GuidHelper.GenerateId(input.Address, dto.PoolId), out var earned)
                    ? earned
                    : "0";
            dto.RealEarned = (decimal.Parse(dto.Earned) * decimal.Parse("0.9")).ToString(CultureInfo.InvariantCulture);
            dto.DailyRewards = dto.TotalStake == "0"
                ? "0"
                : Math.Floor(10000 * 30 / decimal.Parse(dto.TotalStake) * dto.PoolDailyRewards * 100000000)
                    .ToString(CultureInfo.InvariantCulture);
        });
        var sortedPointsPools = pointsPoolsDtos.OrderByDescending(dto => decimal.Parse(dto.DailyRewards)).ToList();

        var result = input.Type == PoolQueryType.Staked
            ? sortedPointsPools.Where(x => x.Staked != "0").ToList()
            : sortedPointsPools;
        if (string.IsNullOrEmpty(input.Address))
        {
            await RedisDatabase.StringSetAsync(PointsPoolUnLoginListRedisKeyPrefix + input.Type + ":" + dappId,
                _serializer.Serialize(result),
                TimeSpan.FromHours(2));
        }

        return result;
    }

    public async Task<ClaimAmountSignatureDto> ClaimAmountSignatureAsync(ClaimAmountSignatureInput input)
    {
        var poolId = input.PoolId;
        var address = input.Address;
        var amount = input.Amount;

        //prevention of duplicate claims
        await using var handle = await _distributedLock.TryAcquireAsync(name: LockKeyPrefix + address);

        if (handle == null)
        {
            throw new UserFriendlyException("generating signature.");
        }

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var id = GuidHelper.GenerateId(address, poolId, today);
        var pointsPoolClaimRecordGrain = _clusterClient.GetGrain<IPointsPoolClaimRecordGrain>(id);
        var record = await pointsPoolClaimRecordGrain.GetAsync();
        if (record != null)
        {
            _logger.LogWarning(
                "already generated signature. id: {id}", id);
            return new ClaimAmountSignatureDto
            {
                Seed = HashHelper.ComputeFrom(record.Seed).ToHex(),
                Signature = ByteStringHelper.FromHexString(record.Signature),
                ExpirationTime = record.ExpiredTime / 1000
            };
        }

        //prevention of over claim
        var addressStakeRewardsDic = await _pointsStakingProvider.GetAddressStakeRewardsDicAsync(address);
        if (!addressStakeRewardsDic.TryGetValue(GuidHelper.GenerateId(address, poolId), out var earned) ||
            Math.Floor(decimal.Parse(earned) * 100000000) - amount < 0 || amount < 0)
        {
            throw new UserFriendlyException("invalid amount");
        }

        //get claiming record
        var claimingList = await _pointsStakingProvider.GetClaimingListAsync(address, poolId);
        //check seeds is claimed
        var seeds = claimingList.Select(x => HashHelper.ComputeFrom(x.Seed).ToHex()).ToList();
        var realClaimInfoList = await _pointsStakingProvider.GetRealClaimInfoListAsync(seeds, address, poolId);
        var realClaimSeeds = realClaimInfoList.Select(x => x.Seed).ToList();
        foreach (var claimingRecord in claimingList.Where(
                     claimingRecord => realClaimSeeds.Contains(HashHelper.ComputeFrom(claimingRecord.Seed).ToHex())))
        {
            //change record status
            await UpdateClaimStatusAsync(address, poolId, claimingRecord.Id, "");
            //sub amount   819135044

            await SettleRewardsAsync(address, poolId,
                -decimal.Parse(claimingRecord.Amount.ToString()) / decimal.Parse("100000000"));
            amount -= claimingRecord.Amount;
        }

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
        var signature = await GenerateSignatureByPubKeyAsync(projectInfo.PublicKey,
            Hash.LoadFromHex(poolId), amount, Address.FromBase58(address),
            HashHelper.ComputeFrom(seed), expiredTime / 1000);

        if (string.IsNullOrEmpty(signature))
        {
            throw new UserFriendlyException("generate signature fail.");
        }

        //save signature
        var claimRecordDto = new PointsPoolClaimRecordDto()
        {
            Id = id,
            Amount = amount,
            PoolId = poolId,
            Address = address,
            Seed = seed,
            Signature = signature,
            ClaimStatus = ClaimStatus.Claiming,
            ExpiredTime = expiredTime
        };

        var saveResult = await pointsPoolClaimRecordGrain.CreateAsync(claimRecordDto);

        if (!saveResult.Success)
        {
            _logger.LogError(
                "save claim record fail, message:{message}, id: {id}", saveResult.Message, id);
            throw new UserFriendlyException(saveResult.Message);
        }

        await _distributedEventBus.PublishAsync(
            _objectMapper.Map<PointsPoolClaimRecordDto, PointsPoolClaimRecordEto>(saveResult.Data));

        return new ClaimAmountSignatureDto
        {
            Seed = HashHelper.ComputeFrom(seed).ToHex(),
            Signature = ByteStringHelper.FromHexString(signature),
            ExpirationTime = expiredTime / 1000
        };
    }

    public async Task<string> ClaimAsync(PointsClaimInput input)
    {
        var transaction =
            Transaction.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(input.RawTransaction));

        var claimInput = new ClaimInput();
        if (transaction.To.ToBase58() == _earnContractOptions.CAContractAddress &&
            transaction.MethodName == "ManagerForwardCall")
        {
            var managerForwardCallInput = ManagerForwardCallInput.Parser.ParseFrom(transaction.Params);
            if (managerForwardCallInput.MethodName == "Claim" &&
                managerForwardCallInput.ContractAddress.ToBase58() == _earnContractOptions.EcoEarnContractAddress)
            {
                claimInput = ClaimInput.Parser.ParseFrom(managerForwardCallInput.Args);
            }
        }
        else if (transaction.To.ToBase58() == _earnContractOptions.EcoEarnContractAddress &&
                 transaction.MethodName == "Claim")
        {
            claimInput = ClaimInput.Parser.ParseFrom(transaction.Params);
        }
        else
        {
            throw new UserFriendlyException("Invalid transaction");
        }

        var publicKey = RecoverPublicKeyFromSignature(claimInput);
        if (!_projectKeyPairInfoOptions.ProjectKeyPairInfos.TryGetValue(CommonConstant.Project, out var pubKey)
            || pubKey.PublicKey != publicKey)
        {
            throw new UserFriendlyException("invalid Signature");
        }

        if (claimInput.ExpirationTime * 1000 < DateTime.UtcNow.ToUtcMilliSeconds())
        {
            throw new UserFriendlyException("Please wait for the reward to be settled");
        }

        var transactionOutput = await _contractProvider.SendTransactionAsync(input.ChainId, transaction);
        var transactionResult =
            await _contractProvider.CheckTransactionStatusAsync(transactionOutput.TransactionId, input.ChainId);
        if (!transactionResult.Result)
        {
            throw new UserFriendlyException(transactionResult.Error);
        }

        var poolId = claimInput.PoolId.ToHex();
        var address = claimInput.Account.ToBase58();

        await SettleRewardsAsync(address, poolId,
            -decimal.Parse(claimInput.Amount.ToString()) / decimal.Parse("100000000"));
        await UpdateClaimStatusAsync(address, poolId, "", DateTime.UtcNow.ToString("yyyyMMdd"));

        return transactionOutput.TransactionId;
    }

    public async Task<List<EarlyStakeInfoDto>> GetEarlyStakeInfoAsync(GetEarlyStakeInfoInput input)
    {
        return await _tokenStakingService.GetStakedInfoAsync(input);
    }

    public async Task<AddressRewardsDto> GetAddressRewardsAsync(GetAddressRewardsInput input)
    {
        var pointsStakeRewardsList = await _pointsStakingProvider.GetAddressRewardsAsync(input.Address);
        return new AddressRewardsDto
        {
            Reward = pointsStakeRewardsList.ToDictionary(x => x.PoolName, x => x.Rewards)
        };
    }

    private async Task UpdateClaimStatusAsync(string address, string poolId, string oldId, string date)
    {
        var id = !string.IsNullOrEmpty(oldId) ? oldId : GuidHelper.GenerateId(address, poolId, date);

        var pointsPoolClaimRecordGrain = _clusterClient.GetGrain<IPointsPoolClaimRecordGrain>(id);

        var saveResult = await pointsPoolClaimRecordGrain.ClaimedAsync();

        if (!saveResult.Success)
        {
            _logger.LogError(
                "update claim statue fail, message:{message}, id: {id}", saveResult.Message, id);
            throw new UserFriendlyException(saveResult.Message);
        }

        await _distributedEventBus.PublishAsync(
            _objectMapper.Map<PointsPoolClaimRecordDto, PointsPoolClaimRecordEto>(saveResult.Data));
    }

    private async Task SettleRewardsAsync(string address, string poolId, decimal claimAmount)
    {
        await ConnectAsync();
        await RedisDatabase.KeyDeleteAsync(PointsPoolStakeRewardsRedisKeyPrefix + address);
        var id = GuidHelper.GenerateId(address, poolId);
        var rewardsSumDto = new PointsStakeRewardsSumDto()
        {
            Id = id,
            PoolId = poolId,
            Rewards = claimAmount.ToString(CultureInfo.InvariantCulture)
        };
        _logger.LogInformation("update rewards sum: {info}", JsonConvert.SerializeObject(rewardsSumDto));
        var rewardsSumGrain = _clusterClient.GetGrain<IPointsStakeRewardsSumGrain>(id);
        var result = await rewardsSumGrain.CreateOrUpdateAsync(rewardsSumDto);

        if (!result.Success)
        {
            _logger.LogError(
                "claim points rewards fail, message:{message}, rewardsId: {rewardsId}",
                result.Message, id);
            throw new UserFriendlyException(result.Message);
        }

        var eto = _objectMapper.Map<PointsStakeRewardsSumDto, PointsStakeRewardsSumEto>(result.Data);
        var listEto = new PointsStakeRewardsSumListEto { EventDataList = new List<PointsStakeRewardsSumEto> { eto } };
        await _distributedEventBus.PublishAsync(listEto);
    }

    private string RecoverPublicKeyFromSignature(ClaimInput input)
    {
        var computedHash = ComputeConfirmInputHash(input);
        if (!CryptoHelper.RecoverPublicKey(input.Signature.ToByteArray(), computedHash.ToByteArray(),
                out var publicKey))
        {
            throw new UserFriendlyException("invalid Signature");
        }

        return publicKey.ToHex();
    }

    private Hash ComputeConfirmInputHash(ClaimInput input)
    {
        return HashHelper.ComputeFrom(new ClaimInput
        {
            PoolId = input.PoolId,
            Account = input.Account,
            Amount = input.Amount,
            Seed = input.Seed,
            ExpirationTime = input.ExpirationTime
        }.ToByteArray());
    }

    private async Task<Dictionary<string, ProjectItemAggDto>> GetProjectItemAggDataDic()
    {
        var snapshotDate = DateTime.UtcNow.ToString("yyyyMMdd");
        await ConnectAsync();
        var redisValue = await RedisDatabase.StringGetAsync(PointsPoolNowSnapshotRedisKeyPrefix + snapshotDate);
        if (redisValue.HasValue)
        {
            return _serializer.Deserialize<Dictionary<string, ProjectItemAggDto>>(redisValue);
        }
        
        var pointsPoolStakeSumList = await _pointsStakingProvider.GetPointsPoolStakeSumAsync();
        
        var projectItemAggDataDic = pointsPoolStakeSumList.GroupBy(x => x.DappId)
            .ToDictionary(g => g.Key, g =>
            {
                var tvl = g.Select(x => BigInteger.Parse(x.StakeAmount))
                    .Aggregate(BigInteger.Zero, (acc, num) => acc + num);
                return tvl;
            });

        var result = new Dictionary<string, ProjectItemAggDto>();
        foreach (var (dappId, tvl) in projectItemAggDataDic)
        {
            var stakingAddress = await _pointsStakingProvider.GetProjectItemAggDataAsync(snapshotDate, dappId);
            result[dappId] = new ProjectItemAggDto
            {
                StakingAddress = stakingAddress,
                Tvl = tvl.ToString()
            };
        }

        await RedisDatabase.StringSetAsync(PointsPoolNowSnapshotRedisKeyPrefix + snapshotDate,
            _serializer.Serialize(result), TimeSpan.FromHours(25));

        return result;
    }

    private async Task<Dictionary<string, string>> GetAddressStakeAmountDicAsync(string address, string dappId)
    {
        await ConnectAsync();
        var redisValue =
            await RedisDatabase.StringGetAsync(PointsPoolStakeAmountRedisKeyPrefix + address + ":" + dappId);
        if (redisValue.HasValue)
        {
            return _serializer.Deserialize<Dictionary<string, string>>(redisValue);
        }

        var result = await _pointsStakingProvider.GetAddressStakeAmountDicAsync(address, dappId);
        await RedisDatabase.StringSetAsync(PointsPoolStakeAmountRedisKeyPrefix + address + ":" + dappId,
            _serializer.Serialize(result), TimeSpan.FromMinutes(10));
        return result;
    }

    private async Task<Dictionary<string, string>> GetAddressStakeRewardsDicAsync(string address, string dappId)
    {
        await ConnectAsync();
        var redisValue =
            await RedisDatabase.StringGetAsync(PointsPoolStakeRewardsRedisKeyPrefix + address + ":" + dappId);
        if (redisValue.HasValue)
        {
            return _serializer.Deserialize<Dictionary<string, string>>(redisValue);
        }

        var result = await _pointsStakingProvider.GetAddressStakeRewardsDicAsync(address, dappId);
        await RedisDatabase.StringSetAsync(PointsPoolStakeRewardsRedisKeyPrefix + address + ":" + dappId,
            _serializer.Serialize(result), TimeSpan.FromMinutes(1));
        return result;
    }

    private async Task<string> GenerateSignatureByPubKeyAsync(string pubKey, Hash poolId, long amount, Address account,
        Hash seed, long expirationTime)
    {
        var data = new ClaimInput
        {
            PoolId = poolId,
            Account = account,
            Amount = amount,
            Seed = seed,
            ExpirationTime = expirationTime
        };
        var dataHash = HashHelper.ComputeFrom(data);
        return await _secretProvider.GetSignatureFromHashAsync(pubKey, dataHash);
    }
}