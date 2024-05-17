using System;
using System.Collections.Generic;
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
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.PointsStakeRewards;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.PointsStaking.Provider;
using EcoEarnServer.TokenStaking;
using FluentAssertions.Extensions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Portkey.Contracts.CA;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.PointsStaking;

public class PointsStakingService : IPointsStakingService, ISingletonDependency
{
    private const string LockKeyPrefix = "EcoEarnServer:PointsRewardsClaim:Lock:";

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

    public PointsStakingService(IOptionsSnapshot<ProjectItemOptions> projectItemOptions, IObjectMapper objectMapper,
        IPointsStakingProvider pointsStakingProvider, IOptionsSnapshot<EcoEarnContractOptions> earnContractOptions,
        ContractProvider contractProvider, IOptionsSnapshot<ProjectKeyPairInfoOptions> projectKeyPairInfoOptions,
        IClusterClient clusterClient, IDistributedEventBus distributedEventBus, ILogger<PointsStakingService> logger,
        ITokenStakingService tokenStakingService, IOptionsSnapshot<ChainOption> chainOption,
        ISecretProvider secretProvider, IAbpDistributedLock distributedLock)
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

    private async Task<Dictionary<string, ProjectItemAggDto>> GetProjectItemAggDataDic()
    {
        var snapshotDate = DateTime.UtcNow.ToString("yyyyMMdd");
        var res = new List<PointsSnapshotIndex>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<PointsSnapshotIndex> list;
        do
        {
            list = await _pointsStakingProvider.GetProjectItemAggDataAsync(snapshotDate, skipCount, maxResultCount);
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < maxResultCount)
            {
                break;
            }

            skipCount += count;
        } while (!list.IsNullOrEmpty());

        var projectItemAggDataDic = res.GroupBy(x => x.DappId)
            .ToDictionary(g => g.Key, g =>
            {
                var firstSymbolSum = g.Select(x => BigInteger.Parse(x.FirstSymbolAmount))
                    .Aggregate(BigInteger.Zero, (acc, num) => acc + num);
                var thirdSymbolSum = g.Select(x => BigInteger.Parse(x.ThirdSymbolAmount))
                    .Aggregate(BigInteger.Zero, (acc, num) => acc + num);
                var fourSymbolSum = g.Select(x => BigInteger.Parse(x.FourSymbolAmount))
                    .Aggregate(BigInteger.Zero, (acc, num) => acc + num);
                var fiveSymbolSum = g.Select(x => BigInteger.Parse(x.FiveSymbolAmount))
                    .Aggregate(BigInteger.Zero, (acc, num) => acc + num);
                var sixSymbolSum = g.Select(x => BigInteger.Parse(x.SixSymbolAmount))
                    .Aggregate(BigInteger.Zero, (acc, num) => acc + num);
                var sevenSymbolSum = g.Select(x => BigInteger.Parse(x.SevenSymbolAmount))
                    .Aggregate(BigInteger.Zero, (acc, num) => acc + num);
                var eightSymbolSum = g.Select(x => BigInteger.Parse(x.EightSymbolAmount))
                    .Aggregate(BigInteger.Zero, (acc, num) => acc + num);
                var nineSymbolSum = g.Select(x => BigInteger.Parse(x.NineSymbolAmount))
                    .Aggregate(BigInteger.Zero, (acc, num) => acc + num);
                var tvl = firstSymbolSum + thirdSymbolSum + fourSymbolSum + fiveSymbolSum + sixSymbolSum +
                          sevenSymbolSum + eightSymbolSum + nineSymbolSum;
                return new ProjectItemAggDto
                {
                    StakingAddress = g.Count(),
                    Tvl = tvl.ToString()
                };
            });
        return projectItemAggDataDic;
    }

    public async Task<List<PointsPoolsDto>> GetPointsPoolsAsync(GetPointsPoolsInput input)
    {
        var pointsPoolsIndexerList = await _pointsStakingProvider.GetPointsPoolsAsync(input.Name);
        var poolIds = pointsPoolsIndexerList.Select(pool => pool.PoolId).ToList();
        var pointsPoolsDtos =
            _objectMapper.Map<List<PointsPoolsIndexerDto>, List<PointsPoolsDto>>(pointsPoolsIndexerList);
        var pointsPoolStakeSumDic = await _pointsStakingProvider.GetPointsPoolStakeSumDicAsync(poolIds);
        var addressStakeAmountDic = await _pointsStakingProvider.GetAddressStakeAmountDicAsync(input.Address);
        var addressStakeRewardsDic = await _pointsStakingProvider.GetAddressStakeRewardsDicAsync(input.Address);
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
        });
        return input.Type == PoolQueryType.Staked
            ? pointsPoolsDtos.Where(x => x.Staked != "0").ToList()
            : pointsPoolsDtos;
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
        // if (!addressStakeRewardsDic.TryGetValue(GuidHelper.GenerateId(address, poolId), out var earned) ||
        //     Math.Floor(decimal.Parse(earned) * 100000000) - amount < 0 || amount < 0)
        // {
        //     throw new UserFriendlyException("invalid amount");
        // }

        //generate signature
        if (!_chainOption.AccountPrivateKey.TryGetValue(ContractConstants.SenderName, out var privateKey))
        {
            throw new UserFriendlyException("invalid pool");
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
        var signature = GenerateSignature(ByteArrayHelper.HexStringToByteArray(privateKey),
            Hash.LoadFromHex(poolId), amount, Address.FromBase58(address),
            HashHelper.ComputeFrom(seed), expiredTime / 1000);

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
        await _distributedEventBus.PublishAsync(_objectMapper.Map<PointsPoolClaimRecordDto, PointsPoolClaimRecordEto>(saveResult.Data));
        
        return new ClaimAmountSignatureDto
        {
            Seed = HashHelper.ComputeFrom(seed).ToHex(),
            Signature = ByteStringHelper.FromHexString(signature),
            ExpirationTime = expiredTime / 1000
        };
    }

    private string GenerateSignature(byte[] privateKey, Hash poolId, long amount, Address account, Hash seed, long expirationTime)
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
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, dataHash.ToByteArray());
        return signature.ToHex();
    }

    private async Task<string> GenerateSignatureByPubKeyAsync(string pubKey, Hash poolId, long amount, Address account,
        Hash seed)
    {
        var data = new ClaimInput
        {
            PoolId = poolId,
            Account = account,
            Amount = amount,
            Seed = seed
        };
        var dataHash = HashHelper.ComputeFrom(data);
        return await _secretProvider.GetSignatureFromHashAsync("", dataHash);
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

        var poolId = claimInput.PoolId.ToHex();
        var address = claimInput.Account.ToBase58();
        var id = GuidHelper.GenerateId(address, poolId);
        var claimAmount = -claimInput.Amount;
        var rewardsSumDto = new PointsStakeRewardsSumDto()
        {
            Id = id,
            PoolId = poolId,
            Rewards = claimAmount.ToString()
        };
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

        var transactionOutput = await _contractProvider.SendTransactionAsync(input.ChainId, transaction);
        return transactionOutput.TransactionId;
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

    public async Task<EarlyStakeInfoDto> GetEarlyStakeInfoAsync(GetEarlyStakeInfoInput input)
    {
        return await _tokenStakingService.GetStakedInfoAsync(input.TokenName, input.Address, input.ChainId);
    }
}