using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AElf.Types;
using EcoEarn.Contracts.Rewards;
using EcoEarnServer.Common;
using EcoEarnServer.Common.AElfSdk;
using EcoEarnServer.Farm.Dtos;
using EcoEarnServer.Farm.Provider;
using EcoEarnServer.Grains.Grain.Rewards;
using EcoEarnServer.Options;
using EcoEarnServer.PointsStaking.Provider;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.Rewards.Provider;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Linq;
using Newtonsoft.Json;
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
    private readonly IFarmProvider _farmProvider;
    private readonly IPriceProvider _priceProvider;
    private readonly PoolInfoOptions _poolInfoOptions;
    private readonly ProjectItemOptions _projectItemOptions;

    public RewardsService(IRewardsProvider rewardsProvider, IObjectMapper objectMapper, ILogger<RewardsService> logger,
        IOptionsSnapshot<TokenPoolIconsOptions> tokenPoolIconsOptions, IPointsStakingProvider pointsStakingProvider,
        ITokenStakingProvider tokenStakingProvider, IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions,
        IAbpDistributedLock distributedLock, IClusterClient clusterClient, IOptionsSnapshot<ChainOption> chainOption,
        IOptionsSnapshot<ProjectKeyPairInfoOptions> projectKeyPairInfoOptions, ISecretProvider secretProvider,
        IDistributedEventBus distributedEventBus, IOptionsSnapshot<EcoEarnContractOptions> earnContractOptions,
        ContractProvider contractProvider, IFarmProvider farmProvider, IPriceProvider priceProvider,
        IOptionsSnapshot<PoolInfoOptions> poolInfoOptions, IOptionsSnapshot<ProjectItemOptions> projectItemOptions)
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
        _farmProvider = farmProvider;
        _priceProvider = priceProvider;
        _projectItemOptions = projectItemOptions.Value;
        _poolInfoOptions = poolInfoOptions.Value;
        _earnContractOptions = earnContractOptions.Value;
        _projectKeyPairInfoOptions = projectKeyPairInfoOptions.Value;
        _chainOption = chainOption.Value;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
        _tokenPoolIconsOptions = tokenPoolIconsOptions.Value;
    }

    public async Task<PagedResultDto<RewardsListDto>> GetRewardsListAsync(GetRewardsListInput input)
    {
        var rewardsListIndexerResult = await _rewardsProvider.GetRewardsInfoListAsync(input.PoolType, input.Address,
            input.Id, input.SkipCount, input.MaxResultCount);
        var result =
            _objectMapper.Map<List<RewardsInfoIndexerDto>, List<RewardsListDto>>(rewardsListIndexerResult.Data);

        if (result.IsNullOrEmpty())
        {
            return new PagedResultDto<RewardsListDto>();
        }
        
        var rewardsToken = result.FirstOrDefault()?.RewardsToken ?? "";
        var currencyPair = $"{rewardsToken}_USDT";
        var rate = await _priceProvider.GetGateIoPriceAsync(currencyPair);

        var poolsIdDic = await GetPoolIdDicAsync(result);
        var dappIdDic = _projectItemOptions.ProjectItems.ToDictionary(x => x.DappId, x => x.DappName);
        foreach (var rewardsListDto in result)
        {
            if (string.IsNullOrEmpty(rewardsListDto.ProjectOwner))
            {
                rewardsListDto.ProjectOwner =
                    dappIdDic.TryGetValue(rewardsListDto.DappId, out var projectOwner) ? projectOwner : "";
            }
            rewardsListDto.TokenIcon =
                _tokenPoolIconsOptions.TokenPoolIconsDic.TryGetValue(
                    rewardsListDto.PoolType == PoolTypeEnums.Points ? rewardsListDto.DappId : rewardsListDto.PoolId,
                    out var icons)
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
            rewardsListDto.RewardsInUsd =
                (double.Parse(rewardsListDto.Rewards) * rate).ToString(CultureInfo.InvariantCulture);
        }

        return new PagedResultDto<RewardsListDto>
        {
            Items = result,
            TotalCount = rewardsListIndexerResult.TotalCount,
        };
    }

    public async Task<List<RewardsAggregationDto>> GetRewardsAggregationAsync(GetRewardsAggregationInput input)
    {
        var result = new List<RewardsAggregationDto>();
        var address = input.Address;
        var poolInfoDic = _poolInfoOptions.PoolInfoDic;
        var rewardsList = await GetAllRewardsList(address, PoolTypeEnums.All);
        var poolIdRewardDic = rewardsList
            .GroupBy(x => x.PoolId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var claimedSymbol = rewardsList.FirstOrDefault()?.ClaimedSymbol ?? "";
        var currencyPair = $"{claimedSymbol}_USDT";
        var usdRate = await _priceProvider.GetGateIoPriceAsync(currencyPair);
        var pointsPools = await _pointsStakingProvider.GetPointsPoolsAsync("");
        var dappIdPointsPools = pointsPools.GroupBy(x => x.DappId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (dappId, dappPointsPools) in dappIdPointsPools)
        {
            var rewardsListIndexerDtos = new List<RewardsListIndexerDto>();
            foreach (var pointsPoolsIndexerDto in dappPointsPools)
            {
                if (poolIdRewardDic.TryGetValue(pointsPoolsIndexerDto.PoolId, out var rewards))
                {
                    rewardsListIndexerDtos.AddRange(rewards);
                }
            }

            if (rewardsListIndexerDtos.IsNullOrEmpty())
            {
                continue;
            }

            var rewardsTokenName = dappPointsPools.FirstOrDefault()?.PointsPoolConfig.RewardToken ?? "";
            var hasPoolInfo = poolInfoDic.TryGetValue(dappId, out var poolInfo);
            var poolRewardsInfoDto = new RewardsAggregationDto
            {
                DappId = dappId,
                PoolName = hasPoolInfo ? poolInfo.PoolName : "",
                Sort = hasPoolInfo ? poolInfo.Sort : 0,
                SupportEarlyStake = hasPoolInfo && poolInfo.SupportEarlyStake,
                RewardsTokenName = rewardsTokenName,
                PoolType = PoolTypeEnums.Points.ToString(),
                PoolTypeEnums = PoolTypeEnums.Points,
            };

            var rewardsAggInfo = await GetRewardsAggAsync(rewardsListIndexerDtos, address, usdRate, "",
                poolRewardsInfoDto.PoolTypeEnums, dappId);
            poolRewardsInfoDto.RewardsInfo = rewardsAggInfo;
            result.Add(poolRewardsInfoDto);
        }

        var tokenPools = await _tokenStakingProvider.GetTokenPoolsAsync(new GetTokenPoolsInput()
        {
            PoolType = PoolTypeEnums.All
        });
        foreach (var tokenPool in tokenPools)
        {
            var poolId = tokenPool.PoolId;
            var poolType = tokenPool.PoolType;

            if (!poolIdRewardDic.TryGetValue(poolId, out var rewardsListIndexerDtos))
            {
                continue;
            }

            var rewardsAggInfo =
                await GetRewardsAggAsync(rewardsListIndexerDtos, address, usdRate, poolId, poolType);
            var hasPoolInfo = poolInfoDic.TryGetValue(poolId, out var poolInfo);
            var poolRewardsInfoDto = new RewardsAggregationDto
            {
                DappId = tokenPool.DappId,
                PoolName = hasPoolInfo ? poolInfo.PoolName : "",
                Sort = hasPoolInfo ? poolInfo.Sort : 0,
                Rate = _lpPoolRateOptions.LpPoolRateDic.TryGetValue(tokenPool.TokenPoolConfig.StakeTokenContract,
                    out var poolRate)
                    ? poolRate
                    : 0,
                SupportEarlyStake = hasPoolInfo && poolInfo.SupportEarlyStake,
                PoolId = poolId,
                RewardsTokenName = tokenPool.TokenPoolConfig.RewardToken,
                PoolType = poolType.ToString(),
                PoolTypeEnums = poolType,
                RewardsInfo = rewardsAggInfo
            };

            result.Add(poolRewardsInfoDto);
        }

        if (input.PoolType != PoolTypeEnums.All)
        {
            return result.Where(x => x.PoolTypeEnums == input.PoolType)
                .OrderByDescending(x => x.RewardsInfo.FirstClaimTime)
                .ToList();
        }

        {
            var pointList = result.Where(x => x.PoolTypeEnums == PoolTypeEnums.Points)
                .OrderByDescending(x => x.RewardsInfo.FirstClaimTime)
                .ToList();
            var tokenList = result.Where(x => x.PoolTypeEnums == PoolTypeEnums.Token)
                .OrderByDescending(x => x.RewardsInfo.FirstClaimTime)
                .ToList();
            var lpList = result.Where(x => x.PoolTypeEnums == PoolTypeEnums.Lp)
                .OrderByDescending(x => x.RewardsInfo.FirstClaimTime)
                .ToList();

            pointList.AddRange(tokenList);
            pointList.AddRange(lpList);
            return pointList;
        }
    }

    public async Task<RewardsSignatureDto> RewardsWithdrawSignatureAsync(RewardsSignatureInput input)
    {
        return await RewardsSignatureAsync(input, ExecuteType.Withdrawn);
    }

    public async Task<string> RewardsWithdrawAsync(RewardsTransactionInput input)
    {
        _logger.LogInformation("RewardsWithdrawAsync, RawTransaction : {rawTransaction}", input.RawTransaction);
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
            throw new UserFriendlyException("Signature Expired");
        }

        var transactionOutput = await _contractProvider.SendTransactionAsync(input.ChainId, transaction);

        var transactionResult =
            await _contractProvider.CheckTransactionStatusAsync(transactionOutput.TransactionId, input.ChainId);
        if (!transactionResult.Result)
        {
            throw new UserFriendlyException(transactionResult.Error);
        }

        await UpdateOperationStatusAsync(withdrawInput.Account.ToBase58(), withdrawInput.ClaimIds);
        return transactionOutput.TransactionId;
    }

    public async Task<RewardsSignatureDto> EarlyStakeSignatureAsync(RewardsSignatureInput input)
    {
        return await RewardsSignatureAsync(input, ExecuteType.EarlyStake);
    }

    public async Task<string> EarlyStakeAsync(RewardsTransactionInput input)
    {
        _logger.LogInformation("EarlyStakeAsync, RawTransaction : {rawTransaction}", input.RawTransaction);

        var transaction =
            Transaction.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(input.RawTransaction));

        var earlyStakeInput = new EarlyStakeInput();
        if (transaction.To.ToBase58() == _earnContractOptions.CAContractAddress &&
            transaction.MethodName == "ManagerForwardCall")
        {
            var managerForwardCallInput = ManagerForwardCallInput.Parser.ParseFrom(transaction.Params);
            if (managerForwardCallInput.MethodName == "EarlyStake" &&
                managerForwardCallInput.ContractAddress.ToBase58() ==
                _earnContractOptions.EcoEarnRewardsContractAddress)
            {
                earlyStakeInput = EarlyStakeInput.Parser.ParseFrom(managerForwardCallInput.Args);
            }
        }
        else if (transaction.To.ToBase58() == _earnContractOptions.EcoEarnRewardsContractAddress &&
                 transaction.MethodName == "EarlyStake")
        {
            earlyStakeInput = EarlyStakeInput.Parser.ParseFrom(transaction.Params);
        }
        else
        {
            throw new UserFriendlyException("Invalid transaction");
        }


        var computedHash = HashHelper.ComputeFrom(new EarlyStakeInput
        {
            StakeInput = earlyStakeInput.StakeInput,
        }.ToByteArray());
        if (!CryptoHelper.RecoverPublicKey(earlyStakeInput.Signature.ToByteArray(), computedHash.ToByteArray(),
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

        if (earlyStakeInput.StakeInput.ExpirationTime * 1000 < DateTime.UtcNow.ToUtcMilliSeconds())
        {
            throw new UserFriendlyException("Signature Expired");
        }

        var transactionOutput = await _contractProvider.SendTransactionAsync(input.ChainId, transaction);

        var transactionResult =
            await _contractProvider.CheckTransactionStatusAsync(transactionOutput.TransactionId, input.ChainId);
        if (!transactionResult.Result)
        {
            throw new UserFriendlyException(transactionResult.Error);
        }

        await UpdateOperationStatusAsync(earlyStakeInput.StakeInput.Account.ToBase58(),
            earlyStakeInput.StakeInput.ClaimIds);
        return transactionOutput.TransactionId;
    }

    public async Task<RewardsSignatureDto> AddLiquiditySignatureAsync(RewardsSignatureInput input)
    {
        return await RewardsSignatureAsync(input, ExecuteType.LiquidityAdded);
    }

    public async Task<string> AddLiquidityAsync(RewardsTransactionInput input)
    {
        _logger.LogInformation("AddLiquidityAsync, RawTransaction : {rawTransaction}", input.RawTransaction);
        var transaction =
            Transaction.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(input.RawTransaction));

        var addLiquidityAndStakeInput = new AddLiquidityAndStakeInput();
        if (transaction.To.ToBase58() == _earnContractOptions.CAContractAddress &&
            transaction.MethodName == "ManagerForwardCall")
        {
            var managerForwardCallInput = ManagerForwardCallInput.Parser.ParseFrom(transaction.Params);
            if (managerForwardCallInput.MethodName == "AddLiquidityAndStake" &&
                managerForwardCallInput.ContractAddress.ToBase58() ==
                _earnContractOptions.EcoEarnRewardsContractAddress)
            {
                addLiquidityAndStakeInput = AddLiquidityAndStakeInput.Parser.ParseFrom(managerForwardCallInput.Args);
            }
        }
        else if (transaction.To.ToBase58() == _earnContractOptions.EcoEarnRewardsContractAddress &&
                 transaction.MethodName == "AddLiquidityAndStake")
        {
            addLiquidityAndStakeInput = AddLiquidityAndStakeInput.Parser.ParseFrom(transaction.Params);
        }
        else
        {
            throw new UserFriendlyException("Invalid transaction");
        }


        var computedHash = HashHelper.ComputeFrom(new AddLiquidityAndStakeInput
        {
            StakeInput = addLiquidityAndStakeInput.StakeInput,
            TokenAMin = addLiquidityAndStakeInput.TokenAMin,
            TokenBMin = addLiquidityAndStakeInput.TokenBMin,
        }.ToByteArray());
        if (!CryptoHelper.RecoverPublicKey(addLiquidityAndStakeInput.Signature.ToByteArray(),
                computedHash.ToByteArray(),
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

        if (addLiquidityAndStakeInput.StakeInput.ExpirationTime * 1000 < DateTime.UtcNow.ToUtcMilliSeconds())
        {
            throw new UserFriendlyException("Signature Expired");
        }

        var transactionOutput = await _contractProvider.SendTransactionAsync(input.ChainId, transaction);

        var transactionResult =
            await _contractProvider.CheckTransactionStatusAsync(transactionOutput.TransactionId, input.ChainId);
        if (!transactionResult.Result)
        {
            throw new UserFriendlyException(transactionResult.Error);
        }

        await UpdateOperationStatusAsync(addLiquidityAndStakeInput.StakeInput.Account.ToBase58(),
            addLiquidityAndStakeInput.StakeInput.ClaimIds);
        return transactionOutput.TransactionId;
    }

    public async Task<bool> CancelSignatureAsync(RewardsSignatureInput input)
    {
        var address = input.Address;
        var ids = input.ClaimInfos.Any() ? input.ClaimInfos.Select(x => x.ClaimId).ToList() : input.LiquidityIds;
        var idsArray = ids.SelectMany(id => Encoding.UTF8.GetBytes(id)).ToArray();
        string idsHex;
        using (var md5 = MD5.Create())
        {
            var hashBytes = md5.ComputeHash(idsArray);
            idsHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        if (string.IsNullOrEmpty(idsHex))
        {
            throw new UserFriendlyException("invalid params");
        }

        var id = input.ClaimInfos.Any() ? GuidHelper.GenerateId(address, idsHex) : GuidHelper.GenerateId(idsHex);
        var rewardOperationRecordGrain = _clusterClient.GetGrain<IRewardOperationRecordGrain>(id);
        var saveResult = await rewardOperationRecordGrain.CancelAsync();

        if (!saveResult.Success)
        {
            _logger.LogError(
                "cancel signature fail, message:{message}, id: {id}", saveResult.Message, id);
            throw new UserFriendlyException(saveResult.Message);
        }

        await _distributedEventBus.PublishAsync(
            _objectMapper.Map<RewardOperationRecordDto, RewardOperationRecordEto>(saveResult.Data));

        return true;
    }

    public async Task<RewardsSignatureDto> LiquidityStakeSignatureAsync(LiquiditySignatureInput input)
    {
        return await LiquiditySignatureAsync(input, ExecuteType.LiquidityStake);
    }

    public async Task<string> LiquidityStakeAsync(RewardsTransactionInput input)
    {
        _logger.LogInformation("LiquidityStakeAsync, RawTransaction : {rawTransaction}", input.RawTransaction);
        var transaction =
            Transaction.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(input.RawTransaction));

        var stakeLiquidityInput = new StakeLiquidityInput();
        if (transaction.To.ToBase58() == _earnContractOptions.CAContractAddress &&
            transaction.MethodName == "ManagerForwardCall")
        {
            var managerForwardCallInput = ManagerForwardCallInput.Parser.ParseFrom(transaction.Params);
            if (managerForwardCallInput.MethodName == "StakeLiquidity" &&
                managerForwardCallInput.ContractAddress.ToBase58() ==
                _earnContractOptions.EcoEarnRewardsContractAddress)
            {
                stakeLiquidityInput = StakeLiquidityInput.Parser.ParseFrom(managerForwardCallInput.Args);
            }
        }
        else if (transaction.To.ToBase58() == _earnContractOptions.EcoEarnRewardsContractAddress &&
                 transaction.MethodName == "StakeLiquidity")
        {
            stakeLiquidityInput = StakeLiquidityInput.Parser.ParseFrom(transaction.Params);
        }
        else
        {
            throw new UserFriendlyException("Invalid transaction");
        }

        var computedHash = HashHelper.ComputeFrom(new StakeLiquidityInput
        {
            LiquidityInput = stakeLiquidityInput.LiquidityInput,
            PoolId = stakeLiquidityInput.PoolId,
            Period = stakeLiquidityInput.Period,
        }.ToByteArray());
        if (!CryptoHelper.RecoverPublicKey(stakeLiquidityInput.Signature.ToByteArray(),
                computedHash.ToByteArray(),
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

        if (stakeLiquidityInput.LiquidityInput.ExpirationTime * 1000 < DateTime.UtcNow.ToUtcMilliSeconds())
        {
            throw new UserFriendlyException("Signature Expired");
        }

        var transactionOutput = await _contractProvider.SendTransactionAsync(input.ChainId, transaction);

        var transactionResult =
            await _contractProvider.CheckTransactionStatusAsync(transactionOutput.TransactionId, input.ChainId);
        if (!transactionResult.Result)
        {
            throw new UserFriendlyException(transactionResult.Error);
        }

        await UpdateOperationStatusAsync(ExecuteType.LiquidityStake.ToString(),
            stakeLiquidityInput.LiquidityInput.LiquidityIds);
        return transactionOutput.TransactionId;
    }

    public async Task<RewardsSignatureDto> RemoveLiquiditySignatureAsync(LiquiditySignatureInput input)
    {
        return await LiquiditySignatureAsync(input, ExecuteType.LiquidityRemove);
    }

    public async Task<string> RemoveLiquidityAsync(RewardsTransactionInput input)
    {
        _logger.LogInformation("RemoveLiquidityAsync, RawTransaction : {rawTransaction}", input.RawTransaction);
        var transaction =
            Transaction.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(input.RawTransaction));

        var removeLiquidityInput = new RemoveLiquidityInput();
        if (transaction.To.ToBase58() == _earnContractOptions.CAContractAddress &&
            transaction.MethodName == "ManagerForwardCall")
        {
            var managerForwardCallInput = ManagerForwardCallInput.Parser.ParseFrom(transaction.Params);
            if (managerForwardCallInput.MethodName == "RemoveLiquidity" &&
                managerForwardCallInput.ContractAddress.ToBase58() ==
                _earnContractOptions.EcoEarnRewardsContractAddress)
            {
                removeLiquidityInput = RemoveLiquidityInput.Parser.ParseFrom(managerForwardCallInput.Args);
            }
        }
        else if (transaction.To.ToBase58() == _earnContractOptions.EcoEarnRewardsContractAddress &&
                 transaction.MethodName == "RemoveLiquidity")
        {
            removeLiquidityInput = RemoveLiquidityInput.Parser.ParseFrom(transaction.Params);
        }
        else
        {
            throw new UserFriendlyException("Invalid transaction");
        }


        var computedHash = HashHelper.ComputeFrom(new RemoveLiquidityInput
        {
            LiquidityInput = removeLiquidityInput.LiquidityInput,
            TokenAMin = removeLiquidityInput.TokenAMin,
            TokenBMin = removeLiquidityInput.TokenBMin,
        }.ToByteArray());
        if (!CryptoHelper.RecoverPublicKey(removeLiquidityInput.Signature.ToByteArray(),
                computedHash.ToByteArray(),
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

        if (removeLiquidityInput.LiquidityInput.ExpirationTime * 1000 < DateTime.UtcNow.ToUtcMilliSeconds())
        {
            throw new UserFriendlyException("Signature Expired");
        }

        var transactionOutput = await _contractProvider.SendTransactionAsync(input.ChainId, transaction);

        var transactionResult =
            await _contractProvider.CheckTransactionStatusAsync(transactionOutput.TransactionId, input.ChainId);
        if (!transactionResult.Result)
        {
            throw new UserFriendlyException(transactionResult.Error);
        }

        await UpdateOperationStatusAsync(ExecuteType.LiquidityRemove.ToString(),
            removeLiquidityInput.LiquidityInput.LiquidityIds);
        return transactionOutput.TransactionId;
    }

    public async Task<List<FilterItemDto>> GetFilterItemsAsync()
    {
        return _poolInfoOptions.PoolInfoDic.Select(x => new FilterItemDto
        {
            FilterName = x.Value.FilterName,
            Id = x.Key,
            PoolType = x.Value.PoolType.ToString(),
            Sort = x.Value.Sort
        }).OrderBy(x => x.Sort).ToList();
    }


    private async Task<RewardsSignatureDto> LiquiditySignatureAsync(LiquiditySignatureInput input,
        ExecuteType executeType)
    {
        var address = input.Address;
        var lpAmount = input.LpAmount;
        var dappId = input.DappId;
        var poolId = input.PoolId;
        var period = input.Period;
        var tokenAMin = input.TokenAMin;
        var tokenBMin = input.TokenBMin;
        var liquidityIds = input.LiquidityIds;


        _logger.LogInformation("LiquiditySignatureAsync, input: {input}", JsonConvert.SerializeObject(input));

        //prevention of duplicate withdraw
        await using var handle = await _distributedLock.TryAcquireAsync(name: LockKeyPrefix + address);

        if (handle == null)
        {
            throw new UserFriendlyException("generating signature.");
        }

        var liquidityIdsArray = liquidityIds.SelectMany(id => Encoding.UTF8.GetBytes(id)).ToArray();
        string liquidityIdsHex;
        using (var md5 = MD5.Create())
        {
            var hashBytes = md5.ComputeHash(liquidityIdsArray);
            liquidityIdsHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        if (string.IsNullOrEmpty(liquidityIdsHex))
        {
            throw new UserFriendlyException("invalid params");
        }

        var id = GuidHelper.GenerateId(liquidityIdsHex);
        var rewardOperationRecordGrain = _clusterClient.GetGrain<IRewardOperationRecordGrain>(id);
        var record = await rewardOperationRecordGrain.GetAsync();
        if (record != null && record.ExecuteStatus != ExecuteStatus.Cancel &&
            record.ExpiredTime > DateTime.UtcNow.ToUtcMilliSeconds())
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

        // //prevention of over withdraw
        // var isValid = await CheckAmountValidityAsync(address, amount, executeClaimIds, poolType, executeType);
        // if (!isValid)
        // {
        //     throw new UserFriendlyException("invalid amount.");
        // }


        var expiredPeriod =
            _projectKeyPairInfoOptions.ProjectKeyPairInfos.TryGetValue(CommonConstant.Project, out var projectInfo)
                ? projectInfo.ExpiredSeconds
                : 180;
        var expiredTime = DateTime.UtcNow
            .AddSeconds(expiredPeriod)
            .ToUtcMilliSeconds();
        var seed = Guid.NewGuid().ToString();
        var repeatedField = new RepeatedField<Hash>();
        repeatedField.AddRange(liquidityIds.Select(Hash.LoadFromHex).ToList());
        IMessage data = executeType switch
        {
            ExecuteType.LiquidityRemove => new RemoveLiquidityInput()
            {
                LiquidityInput = new LiquidityInput
                {
                    LiquidityIds = { repeatedField },
                    LpAmount = lpAmount,
                    DappId = Hash.LoadFromHex(dappId),
                    Seed = HashHelper.ComputeFrom(seed),
                    ExpirationTime = expiredTime / 1000,
                },
                TokenAMin = tokenAMin,
                TokenBMin = tokenBMin,
            },
            ExecuteType.LiquidityStake => new StakeLiquidityInput()
            {
                LiquidityInput = new LiquidityInput
                {
                    LiquidityIds = { repeatedField },
                    LpAmount = lpAmount,
                    DappId = Hash.LoadFromHex(dappId),
                    Seed = HashHelper.ComputeFrom(seed),
                    ExpirationTime = expiredTime / 1000,
                },
                PoolId = Hash.LoadFromHex(poolId),
                Period = period,
            },
            _ => null
        };

        var signature = await GenerateSignatureByPubKeyAsync(projectInfo.PublicKey, data);

        //save signature
        var recordDto = new RewardOperationRecordDto()
        {
            Id = id,
            Amount = lpAmount,
            Address = address,
            Seed = seed,
            Signature = signature,
            LiquidityIds = liquidityIds,
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

    private async Task<RewardsSignatureDto> RewardsSignatureAsync(RewardsSignatureInput input, ExecuteType executeType)
    {
        var poolType = input.PoolType;
        var address = input.Address;
        var amount = input.Amount;
        var executeClaimIds = input.ClaimInfos.Select(x => x.ClaimId).ToList();
        var longestReleaseTime = input.ClaimInfos.Select(x => x.ReleaseTime).Max() / 1000;
        var dappId = input.DappId;
        var poolId = input.PoolId;
        var period = input.Period;
        var tokenAMin = input.TokenAMin;
        var tokenBMin = input.TokenBMin;
        var operationPoolIds = input.OperationPoolIds.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var dappIds = input.OperationDappIds.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();


        _logger.LogInformation("RewardsSignatureAsync, input: {input}", JsonConvert.SerializeObject(input));

        //prevention of duplicate withdraw
        await using var handle = await _distributedLock.TryAcquireAsync(name: LockKeyPrefix + address);

        if (handle == null)
        {
            throw new UserFriendlyException("generating signature.");
        }

        var claimIdsArray = executeClaimIds.SelectMany(id => Encoding.UTF8.GetBytes(id)).ToArray();
        string claimIdsHex;
        using (var md5 = MD5.Create())
        {
            var hashBytes = md5.ComputeHash(claimIdsArray);
            claimIdsHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        if (string.IsNullOrEmpty(claimIdsHex))
        {
            throw new UserFriendlyException("invalid params");
        }

        var id = GuidHelper.GenerateId(address, claimIdsHex);
        var rewardOperationRecordGrain = _clusterClient.GetGrain<IRewardOperationRecordGrain>(id);
        var record = await rewardOperationRecordGrain.GetAsync();
        if (record != null && record.ExecuteStatus != ExecuteStatus.Cancel &&
            record.ExpiredTime > DateTime.UtcNow.ToUtcMilliSeconds())
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
        var isValid = await CheckAmountValidityV2Async(address, amount, executeClaimIds, executeType, operationPoolIds,
            poolType, dappIds);
        if (!isValid)
        {
            throw new UserFriendlyException("invalid amount.");
        }

        var expiredPeriod =
            _projectKeyPairInfoOptions.ProjectKeyPairInfos.TryGetValue(CommonConstant.Project, out var projectInfo)
                ? projectInfo.ExpiredSeconds
                : 180;
        var expiredTime = DateTime.UtcNow
            .AddSeconds(expiredPeriod)
            .ToUtcMilliSeconds();
        var seed = Guid.NewGuid().ToString();
        var repeatedField = new RepeatedField<Hash>();
        repeatedField.AddRange(executeClaimIds.Select(Hash.LoadFromHex).ToList());
        IMessage data = executeType switch
        {
            ExecuteType.Withdrawn => new WithdrawInput
            {
                ClaimIds = { repeatedField },
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
                    ClaimIds = { repeatedField },
                    Account = Address.FromBase58(address),
                    Amount = amount,
                    Seed = HashHelper.ComputeFrom(seed),
                    ExpirationTime = expiredTime / 1000,
                    PoolId = Hash.LoadFromHex(poolId),
                    Period = period,
                    DappId = Hash.LoadFromHex(dappId),
                    LongestReleaseTime = longestReleaseTime,
                }
            },
            ExecuteType.LiquidityAdded => new AddLiquidityAndStakeInput
            {
                StakeInput = new StakeInput
                {
                    ClaimIds = { repeatedField },
                    Account = Address.FromBase58(address),
                    Amount = amount,
                    Seed = HashHelper.ComputeFrom(seed),
                    ExpirationTime = expiredTime / 1000,
                    PoolId = Hash.LoadFromHex(poolId),
                    Period = period,
                    DappId = Hash.LoadFromHex(dappId),
                    LongestReleaseTime = longestReleaseTime,
                },
                TokenAMin = tokenAMin,
                TokenBMin = tokenBMin,
            },
            _ => null
        };

        var signature = await GenerateSignatureByPubKeyAsync(projectInfo.PublicKey, data);

        if (string.IsNullOrEmpty(signature))
        {
            throw new UserFriendlyException("generate signature fail.");
        }

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
        var pointsPoolsIndexerDtos = await _pointsStakingProvider.GetPointsPoolsAsync("", poolIds: pointsPoolIds);
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

    private async Task<bool> CheckAmountValidityV2Async(string address, long amount, List<string> withdrawClaimIds,
        ExecuteType executeType, List<string> poolIds, PoolTypeEnums poolType, List<string> dappIds)
    {
        var rewardsAllList = await GetAllRewardsList(address, poolType, poolIds: poolIds, dappIds: dappIds);
        var unWithdrawList = rewardsAllList
            .Where(x => x.WithdrawTime == 0)
            .ToList();

        var operationRewardsInfo =
            await GetOperationRewardsAsync(unWithdrawList, address, poolIds, poolType: poolType, true, dappIds);
        var nowRewards = operationRewardsInfo.NowRewards;
        var nextReward = operationRewardsInfo.NextRewards;
        var operationClaimInfos = operationRewardsInfo.OperationClaimInfos;
        var rewardOperationRecordList = operationRewardsInfo.RewardOperationRecordList;
        long operationAmount;
        if (executeType == ExecuteType.Withdrawn)
        {
            operationAmount = long.Parse(nowRewards.ClaimedAmount);
        }
        else
        {
            operationAmount = long.Parse(nowRewards.ClaimedAmount) + long.Parse(nextReward.Frozen);
        }

        var resultList = executeType == ExecuteType.Withdrawn
            ? nowRewards.ClaimIds
            : operationClaimInfos.Select(x => x.ClaimId).ToList();
        var includeClaimIds = resultList.Except(withdrawClaimIds).ToList();

        if (includeClaimIds.Any())
        {
            var seeds = rewardOperationRecordList
                .Where(x => x.ExecuteStatus == ExecuteStatus.Executing)
                .Select(x => HashHelper.ComputeFrom(x.Seed).ToHex()).ToList();
            var realClaimInfoList = await _pointsStakingProvider.GetRealClaimInfoListAsync(seeds, address, "");
            var realClaimSeeds = realClaimInfoList.Select(x => x.Seed).ToList();
            foreach (var operationRecord in rewardOperationRecordList.Where(operationRecord =>
                         realClaimSeeds.Contains(HashHelper.ComputeFrom(operationRecord.Seed).ToHex())))
            {
                await UpdateOperationStatusByIdAsync(operationRecord.Id);
            }
        }

        var checkResult = resultList.Count == withdrawClaimIds.Count && !includeClaimIds.Any() &&
                          amount == operationAmount;
        return checkResult;
    }

    private async Task<RewardsAggDto> GetRewardsAggAsync(List<RewardsListIndexerDto> list, string address,
        double usdRate, string poolId, PoolTypeEnums poolType, string dappId = "")
    {
        var pointsPoolAggDto = new RewardsAggDto();
        if (list.IsNullOrEmpty())
        {
            return pointsPoolAggDto;
        }

        var firstClaimTime = list.Min(x => x.ClaimedTime);
        pointsPoolAggDto.RewardsTokenName = list.FirstOrDefault()?.ClaimedSymbol ?? "";
        pointsPoolAggDto.FirstClaimTime = firstClaimTime;

        var totalRewards = list
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        //withdrawn rewards
        var withdrawn = list.Where(x => x.WithdrawTime != 0)
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        pointsPoolAggDto.Withdrawn = withdrawn;
        pointsPoolAggDto.WithdrawnInUsd = (double.Parse(withdrawn) * usdRate).ToString(CultureInfo.InvariantCulture);

        var unWithdrawList = list
            .Where(x => x.WithdrawTime == 0)
            .ToList();
        var poolIds = !string.IsNullOrEmpty(poolId) ? new List<string> { poolId } : null;
        var dappIds = !string.IsNullOrEmpty(dappId) ? new List<string> { dappId } : null;
        var operationRewardsInfo =
            await GetOperationRewardsAsync(unWithdrawList, address, poolIds, poolType: poolType, dappIds: dappIds);

        var nowRewards = operationRewardsInfo.NowRewards;
        var nextReward = operationRewardsInfo.NextRewards;
        var lossAmount = operationRewardsInfo.LossAmount;
        var operationClaimInfos = operationRewardsInfo.OperationClaimInfos;
        var earlyStaked = operationRewardsInfo.EarlyStaked;

        var frozen = nextReward.Frozen;
        pointsPoolAggDto.TotalRewards = (BigInteger.Parse(totalRewards) - lossAmount).ToString();
        pointsPoolAggDto.TotalRewardsInUsd =
            (double.Parse(pointsPoolAggDto.TotalRewards) * usdRate).ToString(CultureInfo.InvariantCulture);
        pointsPoolAggDto.Frozen = frozen;
        pointsPoolAggDto.FrozenInUsd =
            (double.Parse(frozen) * usdRate).ToString(CultureInfo.InvariantCulture);
        pointsPoolAggDto.Withdrawable = nowRewards.ClaimedAmount;
        pointsPoolAggDto.WithdrawableInUsd =
            (double.Parse(nowRewards.ClaimedAmount) * usdRate).ToString(CultureInfo.InvariantCulture);
        pointsPoolAggDto.WithdrawableClaimInfos =
            nowRewards.ClaimIds.Select(x => new ClaimInfoDto { ClaimId = x }).ToList();
        pointsPoolAggDto.NextRewardsRelease = nextReward.ReleaseTime;
        pointsPoolAggDto.NextRewardsReleaseAmount = nextReward.ClaimedAmount;
        pointsPoolAggDto.EarlyStakedAmount = earlyStaked;
        pointsPoolAggDto.EarlyStakedAmountInUsd =
            (double.Parse(earlyStaked) * usdRate).ToString(CultureInfo.InvariantCulture);
        pointsPoolAggDto.ClaimInfos = operationClaimInfos.Select(x => new ClaimInfoDto
        {
            ClaimId = x.ClaimId,
            ReleaseTime = x.ReleaseTime
        }).ToList();
        pointsPoolAggDto.AllRewardsRelease =
            unWithdrawList.Any() &&
            unWithdrawList.Select(x => x.ReleaseTime).Max() < DateTime.UtcNow.ToUtcMilliSeconds() && earlyStaked == "0";
        return pointsPoolAggDto;
    }

    private async Task<OperationRewardsDto> GetOperationRewardsAsync(List<RewardsListIndexerDto> unWithdrawList,
        string address, List<string> poolIds, PoolTypeEnums poolType, bool isCheckAmount = false,
        List<string> dappIds = null)
    {
        var rewardsTokenName = unWithdrawList.FirstOrDefault()?.ClaimedSymbol ?? "";
        var executeStatusList = new List<ExecuteStatus> { ExecuteStatus.Ended };
        if (isCheckAmount)
        {
            executeStatusList.Add(ExecuteStatus.Executing);
        }

        var operationRecordList =
            await _rewardsProvider.GetRewardOperationRecordListAsync(address, executeStatusList);
        var rewardOperationRecordList = operationRecordList
            .Where(x => x.ExecuteStatus == ExecuteStatus.Ended || (x.ExecuteStatus == ExecuteStatus.Executing &&
                                                                   x.ExpiredTime > DateTime.UtcNow.ToUtcMilliSeconds()))
            .ToList();
        var rewardOperationRecordClaimIds = rewardOperationRecordList
            .SelectMany(x => x.ClaimInfos.Select(info => info.ClaimId).ToList())
            .Distinct()
            .ToList();
        var operationClaimList = unWithdrawList
            .Where(x => rewardOperationRecordClaimIds.Contains(x.ClaimId))
            .ToList();
        var earlyStakedIds = operationClaimList
            .Where(x => !x.EarlyStakeInfos.IsNullOrEmpty())
            .SelectMany(x => x.EarlyStakeInfos.Select(l => l.StakeId))
            .Distinct()
            .ToList();
        var unLockedStakeIds = await _rewardsProvider.GetUnLockedStakeIdsAsync(earlyStakedIds, address);
        var liquidityIds = operationClaimList
            .Where(x => !x.LiquidityAddedInfos.IsNullOrEmpty())
            .SelectMany(x => x.LiquidityAddedInfos.Select(l => l.LiquidityId))
            .Distinct()
            .ToList();
        var liquidityRemovedList = await GetAllLiquidityInfoAsync(liquidityIds, "", LpStatus.Removed);
        var liquidityRemovedSeeds = liquidityRemovedList.Select(x => x.Seed).ToList();
        var filterClaimList = operationClaimList
            .Where(x =>
            {
                var stakeIds = x.EarlyStakeInfos.Select(e => e.StakeId).ToList();
                var allLiquidityIds = x.LiquidityAddedInfos.Select(e => e.LiquidityId).ToList();
                var allLiquidityAddedSeeds = x.LiquidityAddedInfos.Select(e => e.LiquidityAddedSeed).ToList();
                return (stakeIds.All(i => earlyStakedIds.Contains(i)) &&
                        !stakeIds.All(i => unLockedStakeIds.Contains(i))) ||
                       (allLiquidityIds.All(i => liquidityIds.Contains(i)) &&
                        !allLiquidityAddedSeeds.All(i => liquidityRemovedSeeds.Contains(i)));
            }).ToList();
        var shouldRemoveClaimIds = filterClaimList.Select(x => x.ClaimId).ToList();
        var realList = unWithdrawList.Where(x => !shouldRemoveClaimIds.Contains(x.ClaimId))
            .OrderBy(x => x.ReleaseTime)
            .ToList();
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        var liquidityAddedInfoDtos = realList.SelectMany(x => x.LiquidityAddedInfos);
        var unWithdrawLiquidityAddedInfoDtos = unWithdrawList.SelectMany(x => x.LiquidityAddedInfos);
        var filterClaimLiquidityAddedInfoDtos = filterClaimList.SelectMany(x => x.LiquidityAddedInfos);
        var lossAmount = 0L;
        var lossAmountSum = 0L;
        var earlyStakedLossAmount = 0L;
        if (!liquidityRemovedList.IsNullOrEmpty())
        {
            lossAmount = rewardsTokenName == liquidityRemovedList.FirstOrDefault()?.TokenASymbol
                ? liquidityAddedInfoDtos.Sum(l => long.Parse(l.TokenALossAmount))
                : liquidityAddedInfoDtos.Sum(l => long.Parse(l.TokenBLossAmount));
            lossAmountSum = rewardsTokenName == liquidityRemovedList.FirstOrDefault()?.TokenASymbol
                ? unWithdrawLiquidityAddedInfoDtos.Sum(l => long.Parse(l.TokenALossAmount))
                : unWithdrawLiquidityAddedInfoDtos.Sum(l => long.Parse(l.TokenBLossAmount));
            earlyStakedLossAmount = rewardsTokenName == liquidityRemovedList.FirstOrDefault()?.TokenASymbol
                ? filterClaimLiquidityAddedInfoDtos.Sum(l => long.Parse(l.TokenALossAmount))
                : filterClaimLiquidityAddedInfoDtos.Sum(l => long.Parse(l.TokenBLossAmount));
        }

        var realClaimIds = realList.Select(x => x.ClaimId).ToList();
        var rewardsMergedList = await GetAllMergedRewardsList(address, poolIds, poolType, dappIds);

        var rewardsMergeDtos = new List<RewardsMergeDto>();
        foreach (var rewardsMergedListIndexerDto in rewardsMergedList)
        {
            var mergeDto = new RewardsMergeDto()
            {
                ClaimedAmount = rewardsMergedListIndexerDto.Amount,
                ReleaseTime = rewardsMergedListIndexerDto.ReleaseTime
            };
            var amount = BigInteger.Parse(rewardsMergedListIndexerDto.Amount);
            var claimIds = new List<string>();
            foreach (var mergedClaimInfoIndexerDto in rewardsMergedListIndexerDto.MergeClaimInfos)
            {
                if (!realClaimIds.Contains(mergedClaimInfoIndexerDto.ClaimId))
                {
                    amount -= BigInteger.Parse(mergedClaimInfoIndexerDto.ClaimedAmount);
                }
                else
                {
                    claimIds.Add(mergedClaimInfoIndexerDto.ClaimId);
                }
            }

            mergeDto.ClaimIds = claimIds;
            mergeDto.ClaimedAmount = amount.ToString();
            rewardsMergeDtos.Add(mergeDto);
        }

        var (nowRewards, nextReward) = GetNextReward(rewardsMergeDtos, now, lossAmount);

        var earlyStaked = filterClaimList
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num);

        return new OperationRewardsDto
        {
            NowRewards = nowRewards,
            NextRewards = nextReward,
            LossAmount = BigInteger.Parse(lossAmountSum.ToString()),
            EarlyStaked = (earlyStaked - BigInteger.Parse(earlyStakedLossAmount.ToString())).ToString(),
            RewardOperationRecordList = rewardOperationRecordList,
            OperationClaimInfos = realList.Select(x => new ClaimInfoDto
            {
                ClaimId = x.ClaimId,
                ReleaseTime = x.ReleaseTime
            }).ToList()
        };
    }

    private async Task UpdateOperationStatusByIdAsync(string id)
    {
        var rewardOperationRecordGrain = _clusterClient.GetGrain<IRewardOperationRecordGrain>(id);

        var saveResult = await rewardOperationRecordGrain.EndedAsync();

        if (!saveResult.Success)
        {
            _logger.LogError(
                "update operation statue fail, message:{message}, id: {id}", saveResult.Message, id);
            throw new UserFriendlyException(saveResult.Message);
        }

        await _distributedEventBus.PublishAsync(
            _objectMapper.Map<RewardOperationRecordDto, RewardOperationRecordEto>(saveResult.Data));
    }

    private async Task UpdateOperationStatusAsync(string address, IEnumerable<Hash> claimIds)
    {
        var claimIdsArray = claimIds.SelectMany(claimId => Encoding.UTF8.GetBytes(claimId.ToHex())).ToArray();
        string claimIdsHex;
        using (var md5 = MD5.Create())
        {
            var hashBytes = md5.ComputeHash(claimIdsArray);
            claimIdsHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        var id = GuidHelper.GenerateId(address, claimIdsHex);

        await UpdateOperationStatusByIdAsync(id);
    }

    private static (RewardsMergeDto Now, RewardsMergeDto NextReward) GetNextReward(List<RewardsMergeDto> rewards,
        long nowTime, long lossAmount)
    {
        if (rewards == null || rewards.Count == 0)
        {
            return (new RewardsMergeDto(), new RewardsMergeDto());
        }

        var withdrawableAmount = 0L;
        var futureRewards = rewards.Where(r => r.ReleaseTime > nowTime).OrderBy(r => r.ReleaseTime)
            .ToList();
        var pastRewards = rewards.Where(r => r.ReleaseTime <= nowTime).OrderBy(r => r.ReleaseTime).ToList();

        var wClaimIds = new List<string>();
        var nClaimIds = new List<string>();
        var next = new RewardsMergeDto();
        var now = new RewardsMergeDto();
        long surplus = 0;
        if (pastRewards.Any())
        {
            withdrawableAmount = pastRewards.Sum(r => long.Parse(r.ClaimedAmount));
        }

        // Deduct the loss amount
        if (withdrawableAmount >= lossAmount)
        {
            withdrawableAmount -= lossAmount;
            wClaimIds = pastRewards.SelectMany(x => x.ClaimIds).ToList();
            next = futureRewards.Any() ? futureRewards.First() : new RewardsMergeDto();
            surplus = futureRewards.Sum(x => long.Parse(x.ClaimedAmount)) - long.Parse(next.ClaimedAmount);
        }
        else
        {
            var remainingLoss = lossAmount - withdrawableAmount;
            withdrawableAmount = 0;
            wClaimIds = new List<string>();

            // Deduct from the next reward
            while (remainingLoss > 0 && futureRewards.Any())
            {
                var nextReward = futureRewards.First();
                nClaimIds.AddRange(nextReward.ClaimIds);
                if (long.Parse(nextReward.ClaimedAmount) >= remainingLoss)
                {
                    next.ClaimedAmount = (long.Parse(nextReward.ClaimedAmount) - remainingLoss).ToString();
                    next.ReleaseTime = nextReward.ReleaseTime;
                    remainingLoss = 0;
                }
                else
                {
                    remainingLoss -= long.Parse(nextReward.ClaimedAmount);
                    futureRewards.RemoveAt(0);
                }
            }

            if (long.Parse(next.ClaimedAmount) < 0)
            {
                next.ClaimedAmount = "0";
            }

            next.ClaimIds = nClaimIds;
            surplus = futureRewards.Sum(x => long.Parse(x.ClaimedAmount));
        }

        next.Frozen = (long.Parse(next.ClaimedAmount) + surplus).ToString();

        now.ClaimedAmount = withdrawableAmount.ToString();
        now.ClaimIds = wClaimIds;
        return (now, next);
    }

    private async Task<List<RewardsListIndexerDto>> GetAllRewardsList(string address, PoolTypeEnums poolType,
        List<string> poolIds = null, List<string> dappIds = null)
    {
        var res = new List<RewardsListIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<RewardsListIndexerDto> list;
        do
        {
            var rewardsListIndexerResult = await _rewardsProvider.GetRewardsListAsync(poolType, address,
                skipCount, maxResultCount, poolIds: poolIds, dappIds: dappIds);
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

    private async Task<List<RewardsMergedListIndexerDto>> GetAllMergedRewardsList(string address,
        List<string> poolIds, PoolTypeEnums poolType, List<string> dappIds = null)
    {
        var res = new List<RewardsMergedListIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<RewardsMergedListIndexerDto> list;
        do
        {
            var rewardsListIndexerResult = await _rewardsProvider.GetMergedRewardsListAsync(address, poolIds, poolType,
                dappIds, skipCount, maxResultCount);
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

    private async Task<List<LiquidityInfoIndexerDto>> GetAllLiquidityInfoAsync(List<string> liquidityIds,
        string address, LpStatus lpStatus)
    {
        var res = new List<LiquidityInfoIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<LiquidityInfoIndexerDto> list;
        do
        {
            var rewardsListIndexerResult =
                await _farmProvider.GetLiquidityInfoAsync(liquidityIds, address, lpStatus, skipCount, maxResultCount);
            list = rewardsListIndexerResult;
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