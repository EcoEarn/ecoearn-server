using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AElf.Types;
using EcoEarn.Contracts.Points;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.Grains.Grain.PointsStakeRewards;
using EcoEarnServer.Options;
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.PointsStakeRewards;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.PointsStaking.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Portkey.Contracts.CA;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.PointsStaking;

public class PointsStakingService : IPointsStakingService, ISingletonDependency
{
    private readonly ProjectItemOptions _projectItemOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IPointsStakingProvider _pointsStakingProvider;
    private readonly EcoEarnContractOptions _earnContractOptions;
    private readonly ContractProvider _contractProvider;
    private readonly ProjectKeyPairInfoOptions _projectKeyPairInfoOptions;
    private readonly IClusterClient _clusterClient;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILogger<PointsStakingService> _logger;

    public PointsStakingService(IOptionsSnapshot<ProjectItemOptions> projectItemOptions, IObjectMapper objectMapper,
        IPointsStakingProvider pointsStakingProvider, IOptionsSnapshot<EcoEarnContractOptions> earnContractOptions,
        ContractProvider contractProvider, IOptionsSnapshot<ProjectKeyPairInfoOptions> projectKeyPairInfoOptions,
        IClusterClient clusterClient, IDistributedEventBus distributedEventBus, ILogger<PointsStakingService> logger)
    {
        _objectMapper = objectMapper;
        _pointsStakingProvider = pointsStakingProvider;
        _contractProvider = contractProvider;
        _clusterClient = clusterClient;
        _distributedEventBus = distributedEventBus;
        _logger = logger;
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
            if (!projectItemAggDataDic.TryGetValue(dto.DappName, out var aggData))
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
        var snapshotDate = DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd");
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

        return new Dictionary<string, ProjectItemAggDto>();
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
            dto.Staked = addressStakeAmountDic.TryGetValue(dto.PoolId, out var staked) ? staked : "0";
            dto.Earned = addressStakeRewardsDic.TryGetValue(dto.PoolId, out var earned) ? earned : "0";
        });
        return pointsPoolsDtos;
    }

    public Task<ClaimAmountSignatureDto> ClaimAmountSignatureAsync(ClaimAmountSignatureInput input)
    {
        if (!_projectKeyPairInfoOptions.ProjectKeyPairInfos.TryGetValue(input.PoolId, out var privateKey))
        {
            throw new Exception("invalid pool");
        }

        var seed = Guid.NewGuid().ToString();
        var signature = GenerateSignature(ByteArrayHelper.HexStringToByteArray(privateKey.PrivateKey),
            Hash.LoadFromHex(input.PoolId), input.Amount, Address.FromBase58(input.Address),
            HashHelper.ComputeFrom(seed));

        return Task.FromResult(new ClaimAmountSignatureDto
        {
            Seed = seed,
            Signature = signature
        });
    }

    private string GenerateSignature(byte[] privateKey, Hash poolId, long amount, Address account, Hash seed)
    {
        var data = new ClaimInput
        {
            PoolId = poolId,
            Account = account,
            Amount = amount,
            Seed = seed
        };
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, dataHash.ToByteArray());
        return signature.ToHex();
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
            if (managerForwardCallInput.MethodName == "ApplyToBeAdvocate" &&
                managerForwardCallInput.ContractAddress.ToBase58() == _earnContractOptions.EcoEarnContractAddress)
            {
                claimInput = ClaimInput.Parser.ParseFrom(managerForwardCallInput.Args);
            }
        }
        else if (transaction.To.ToBase58() == _earnContractOptions.EcoEarnContractAddress &&
                 transaction.MethodName == "ApplyToBeAdvocate")
        {
            claimInput = ClaimInput.Parser.ParseFrom(transaction.Params);
        }
        else
        {
            throw new UserFriendlyException("Invalid transaction");
        }

        var poolId = claimInput.PoolId.ToHex();
        var address = claimInput.Account.ToBase58();
        var id = GuidHelper.GenerateId(address, poolId);
        var claimAmount = claimInput.Amount;
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
        await _distributedEventBus.PublishAsync(listEto, false, false);

        var transactionOutput = await _contractProvider.SendTransactionAsync(input.ChainId, transaction);
        return transactionOutput.TransactionId;
    }
}