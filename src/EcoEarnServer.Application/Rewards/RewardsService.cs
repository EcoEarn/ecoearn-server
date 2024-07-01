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

    public RewardsService(IRewardsProvider rewardsProvider, IObjectMapper objectMapper, ILogger<RewardsService> logger,
        IOptionsSnapshot<TokenPoolIconsOptions> tokenPoolIconsOptions, IPointsStakingProvider pointsStakingProvider,
        ITokenStakingProvider tokenStakingProvider, IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions,
        IAbpDistributedLock distributedLock, IClusterClient clusterClient, IOptionsSnapshot<ChainOption> chainOption,
        IOptionsSnapshot<ProjectKeyPairInfoOptions> projectKeyPairInfoOptions, ISecretProvider secretProvider,
        IDistributedEventBus distributedEventBus, IOptionsSnapshot<EcoEarnContractOptions> earnContractOptions,
        ContractProvider contractProvider, IFarmProvider farmProvider)
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

        var pointsPoolsIndexerDtos = await _pointsStakingProvider.GetPointsPoolsAsync("");
        var pointsPoolRewardsToken = pointsPoolsIndexerDtos.FirstOrDefault()?.PointsPoolConfig.RewardToken;
        rewardsAggregationDto.PointsPoolAgg.RewardsTokenName = pointsPoolRewardsToken;
        rewardsAggregationDto.DappId = pointsPoolsIndexerDtos.FirstOrDefault()?.DappId;
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
        if (!transactionResult)
        {
            throw new UserFriendlyException("transaction fail.");
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
        if (!transactionResult)
        {
            throw new UserFriendlyException("transaction fail.");
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
        if (!transactionResult)
        {
            throw new UserFriendlyException("transaction fail.");
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
            LongestReleaseTime = stakeLiquidityInput.LongestReleaseTime,
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
        if (!transactionResult)
        {
            throw new UserFriendlyException("transaction fail.");
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
        if (!transactionResult)
        {
            throw new UserFriendlyException("transaction fail.");
        }

        await UpdateOperationStatusAsync(ExecuteType.LiquidityRemove.ToString(),
            removeLiquidityInput.LiquidityInput.LiquidityIds);
        return transactionOutput.TransactionId;
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
        var rewardsAllList = await GetAllRewardsList(address, PoolTypeEnums.All, liquidityIds);
        var longestReleaseTime = rewardsAllList.Select(x => x.ReleaseTime).Max();
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
                LongestReleaseTime = longestReleaseTime / 1000,
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
        var longestReleaseTime = input.ClaimInfos.Last().ReleaseTime / 1000;
        var dappId = input.DappId;
        var poolId = input.PoolId;
        var period = input.Period;
        var tokenAMin = input.TokenAMin;
        var tokenBMin = input.TokenBMin;


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
        var isValid = await CheckAmountValidityAsync(address, amount, executeClaimIds, poolType, executeType);
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
        _logger.LogInformation("rewardsAllList: {rewardsAllList}", rewardsAllList);

        var rewardOperationRecordAllList = await _rewardsProvider.GetRewardOperationRecordListAsync(address,
            new List<ExecuteStatus> { ExecuteStatus.Executing, ExecuteStatus.Ended });
        var unWithdrawList = rewardsAllList
            .Where(x => x.WithdrawTime == 0)
            .ToList();
        List<RewardsListIndexerDto> pastReleaseTimeClaimInfoList;
        if (executeType == ExecuteType.Withdrawn)
        {
            var rewardsTokenName = unWithdrawList.FirstOrDefault()?.ClaimedSymbol;
            var endedOperationClaimIds = rewardOperationRecordAllList.Where(x => x.ExecuteStatus == ExecuteStatus.Ended)
                .SelectMany(x => x.ClaimInfos.Select(info => info.ClaimId))
                .ToList();
            var operationClaimList = unWithdrawList
                .Where(x => endedOperationClaimIds.Contains(x.ClaimId))
                .ToList();
            var liquidityIds = operationClaimList
                .Where(x => !string.IsNullOrEmpty(x.LiquidityAddedSeed))
                .Select(x => x.LiquidityId)
                .ToList();
            var liquidityRemovedList =
                await _farmProvider.GetLiquidityInfoAsync(liquidityIds, "", LpStatus.Removed, 0, 5000);

            var lossAmount = BigInteger.Zero;
            foreach (var liquidityInfoIndexerDto in liquidityRemovedList)
            {
                if (rewardsTokenName == liquidityInfoIndexerDto.TokenASymbol)
                {
                    lossAmount += BigInteger.Parse(liquidityInfoIndexerDto.TokenALossAmount);
                }
                else
                {
                    lossAmount += BigInteger.Parse(liquidityInfoIndexerDto.TokenBLossAmount);
                }
            }

            var rewardsDtos = unWithdrawList.Select(x => new RewardsDto
            {
                ClaimedAmount = x.ClaimedAmount,
                ClaimId = x.ClaimId,
                ReleaseTime = x.ReleaseTime,
            }).ToList();
            var rewardsMergeDtos = MergeRewards(rewardsDtos, _earnContractOptions.MergeMilliseconds);
            var (nowRewards, nextReward) = GetNextReward(rewardsMergeDtos, DateTime.UtcNow.ToUtcMilliSeconds(),
                long.Parse(lossAmount.ToString()));

            pastReleaseTimeClaimInfoList = unWithdrawList
                .Where(x => nowRewards.ClaimIds.Contains(x.ClaimId))
                .ToList();
        }
        else
        {
            pastReleaseTimeClaimInfoList = unWithdrawList;
        }

        var pastReleaseTimeClaimIds = pastReleaseTimeClaimInfoList
            .Select(x => x.ClaimId)
            .Distinct()
            .ToList();
        _logger.LogInformation("pastReleaseTimeClaimIds: {pastReleaseTimeClaimIds}", pastReleaseTimeClaimIds);


        var rewardOperationRecordList = rewardOperationRecordAllList
            .Where(x => x.ExecuteStatus == ExecuteStatus.Ended || (x.ExecuteStatus == ExecuteStatus.Executing &&
                                                                   x.ExpiredTime > DateTime.UtcNow.ToUtcMilliSeconds()))
            .ToList();

        //withdrawn
        var withdrawnOperationList = rewardOperationRecordList
            .Where(x => x.ExecuteType == ExecuteType.Withdrawn)
            .ToList();
        var withdrawnClaimIds = withdrawnOperationList
            .SelectMany(x => x.ClaimInfos.Select(info => info.ClaimId))
            .ToList();

        //Early Staked
        var earlyStakedOperationList = rewardOperationRecordList
            .Where(x => x.ExecuteType == ExecuteType.EarlyStake)
            .ToList();
        var earlySeeds = earlyStakedOperationList.Select(x => HashHelper.ComputeFrom(x.Seed).ToHex()).ToList();
        var earlyStakedClaimInfoList = await _pointsStakingProvider.GetRealClaimInfoListAsync(earlySeeds, address, "");
        var earlyStakedIds = earlyStakedClaimInfoList
            .Where(x => !string.IsNullOrEmpty(x.EarlyStakeSeed))
            .Select(x => x.StakeId)
            .Distinct().ToList();
        var unLockedStakeIds = await _rewardsProvider.GetUnLockedStakeIdsAsync(earlyStakedIds, address);
        var unlockedClaimInfos = earlyStakedClaimInfoList.Where(x => unLockedStakeIds.Contains(x.StakeId)).ToList();
        var unlockedClaimIds = unlockedClaimInfos.Select(x => x.ClaimId).ToList();

        var earlyStakedClaimIds = earlyStakedOperationList
            .SelectMany(x => x.ClaimInfos.Select(info => info.ClaimId))
            .Where(claimId => !unlockedClaimIds.Contains(claimId))
            .ToList();

        //Liquidity Added
        var liquidityAddedOperationList = rewardOperationRecordList
            .Where(x => x.ExecuteType == ExecuteType.LiquidityAdded)
            .ToList();
        var liquidityAddedSeeds =
            liquidityAddedOperationList.Select(x => HashHelper.ComputeFrom(x.Seed).ToHex()).ToList();
        var liquidityAddedClaimInfoList =
            await _pointsStakingProvider.GetRealClaimInfoListAsync(liquidityAddedSeeds, address, "");
        var liquidityAddedIds = liquidityAddedClaimInfoList
            .Where(x => !string.IsNullOrEmpty(x.LiquidityAddedSeed))
            .Select(x => x.LiquidityId)
            .ToList();
        var liquidityRemovedInfoList =
            await _farmProvider.GetLiquidityInfoAsync(liquidityAddedIds, "", LpStatus.Removed, 0, 5000);
        var removedIds = liquidityRemovedInfoList.Select(x => x.LiquidityId).ToList();
        var removedClaimIds = liquidityAddedClaimInfoList.Where(x => removedIds.Contains(x.LiquidityId))
            .Select(x => x.ClaimId);
        var liquidityAddedClaimIds = liquidityAddedOperationList
            .SelectMany(x => x.ClaimInfos.Select(info => info.ClaimId))
            .Where(claimId => !removedClaimIds.Contains(claimId))
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
        _logger.LogInformation("resultList: {resultList}", resultList);


        var includeClaimIds = resultList.Except(withdrawClaimIds).ToList();

        _logger.LogInformation("includeClaimIds: {includeClaimIds}", includeClaimIds);
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

        var withdrawAmount = pastReleaseTimeClaimInfoList
            .Where(x => resultList.Contains(x.ClaimId))
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        var checkResult = resultList.Count == withdrawClaimIds.Count && !includeClaimIds.Any() &&
                          amount.ToString() == withdrawAmount;

        if (!checkResult)
        {
            _logger.LogWarning(
                "check amount false. resultList: {resultList}, includeClaimIds: {resultList}, " +
                "withdrawAmount: {withdrawAmount}, rewardOperationRecordAllList: {rewardOperationRecordAllList}, rewardOperationRecordList: {rewardOperationRecordList}, excludedClaimIds: {excludedClaimIds}",
                resultList, includeClaimIds, withdrawAmount, rewardOperationRecordAllList, rewardOperationRecordList,
                excludedClaimIds);
        }

        return checkResult;
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


        var withdrawn = list.Where(x => x.WithdrawTime != 0)
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        pointsPoolAggDto.Withdrawn = withdrawn;
        pointsPoolAggDto.WithdrawnInUsd = (double.Parse(withdrawn) * usdRate).ToString(CultureInfo.InvariantCulture);

        var unWithdrawList = list
            .Where(x => x.WithdrawTime == 0)
            .ToList();

        var rewardOperationRecordList =
            await _rewardsProvider.GetRewardOperationRecordListAsync(address,
                new List<ExecuteStatus> { ExecuteStatus.Ended });
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
        var liquidityRemovedList =
            await _farmProvider.GetLiquidityInfoAsync(liquidityIds, "", LpStatus.Removed, 0, 5000);
        var liquidityRemovedSeeds = liquidityRemovedList.Select(x => x.Seed).ToList();
        var shouldRemoveClaimIds = operationClaimList
            .Where(x => !unLockedStakeIds.Contains(x.StakeId) && !liquidityRemovedSeeds.Contains(x.LiquidityAddedSeed))
            .Select(x => x.ClaimId)
            .ToList();

        var realList = unWithdrawList.Where(x => !shouldRemoveClaimIds.Contains(x.ClaimId))
            .OrderBy(x => x.ReleaseTime)
            .ToList();

        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        var lossAmount = BigInteger.Zero;
        foreach (var liquidityInfoIndexerDto in liquidityRemovedList)
        {
            if (pointsPoolAggDto.RewardsTokenName == liquidityInfoIndexerDto.TokenASymbol)
            {
                lossAmount += BigInteger.Parse(liquidityInfoIndexerDto.TokenALossAmount);
            }
            else
            {
                lossAmount += BigInteger.Parse(liquidityInfoIndexerDto.TokenBLossAmount);
            }
        }

        var rewardsDtos = realList.Select(x => new RewardsDto
        {
            ClaimedAmount = x.ClaimedAmount,
            ClaimId = x.ClaimId,
            ReleaseTime = x.ReleaseTime,
        }).ToList();
        var rewardsMergeDtos = MergeRewards(rewardsDtos, _earnContractOptions.MergeMilliseconds);
        var (nowRewards, nextReward) = GetNextReward(rewardsMergeDtos, now, long.Parse(lossAmount.ToString()));

        var frozenList = realList.Where(x => !nowRewards.ClaimIds.Contains(x.ClaimId)).ToList();
        var frozenSum = frozenList
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num);

        pointsPoolAggDto.TotalRewards = (BigInteger.Parse(totalRewards) - lossAmount).ToString();
        pointsPoolAggDto.TotalRewardsInUsd =
            (double.Parse(pointsPoolAggDto.TotalRewards) * usdRate).ToString(CultureInfo.InvariantCulture);
        pointsPoolAggDto.Frozen = frozenSum.ToString();
        pointsPoolAggDto.FrozenInUsd =
            (double.Parse(frozenSum.ToString()) * usdRate).ToString(CultureInfo.InvariantCulture);
        pointsPoolAggDto.Withdrawable = nowRewards.ClaimedAmount;
        pointsPoolAggDto.WithdrawableInUsd =
            (double.Parse(nowRewards.ClaimedAmount) * usdRate).ToString(CultureInfo.InvariantCulture);
        pointsPoolAggDto.WithdrawableClaimInfos =
            nowRewards.ClaimIds.Select(x => new ClaimInfoDto { ClaimId = x }).ToList();
        pointsPoolAggDto.NextRewardsRelease = nextReward.ReleaseTime;
        pointsPoolAggDto.NextRewardsReleaseAmount = nextReward.ClaimedAmount;

        _logger.LogInformation("earlyStakedIds: {earlyStakedIds} , unLockedStakeIds: {unLockedStakeIds}",
            JsonConvert.SerializeObject(earlyStakedIds), JsonConvert.SerializeObject(unLockedStakeIds));
        _logger.LogInformation("liquidityIds: {liquidityIds} , liquidityRemovedStakeIds: {liquidityRemovedStakeIds}",
            JsonConvert.SerializeObject(earlyStakedIds), JsonConvert.SerializeObject(unLockedStakeIds));
        _logger.LogInformation("operationClaimList: {operationClaimList}", JsonConvert.SerializeObject(operationClaimList));
        var earlyStaked = operationClaimList
            .Where(x => (earlyStakedIds.Contains(x.StakeId) && !unLockedStakeIds.Contains(x.StakeId)) ||
                        (liquidityIds.Contains(x.LiquidityId) && !liquidityRemovedSeeds.Contains(x.LiquidityAddedSeed)))
            .Select(x => BigInteger.Parse(x.ClaimedAmount))
            .Aggregate(BigInteger.Zero, (acc, num) => acc + num)
            .ToString();
        pointsPoolAggDto.EarlyStakedAmount = earlyStaked;
        pointsPoolAggDto.EarlyStakedAmountInUsd =
            (double.Parse(earlyStaked) * usdRate).ToString(CultureInfo.InvariantCulture);
        pointsPoolAggDto.ClaimInfos = realList.Select(x => new ClaimInfoDto
        {
            ClaimId = x.ClaimId,
            ReleaseTime = x.ReleaseTime
        }).ToList();
        pointsPoolAggDto.AllRewardsRelease =
            unWithdrawList.Select(x => x.ReleaseTime).Max() < DateTime.UtcNow.ToUtcMilliSeconds();
        return pointsPoolAggDto;
    }

    public static List<RewardsMergeDto> MergeRewards(List<RewardsDto> rewards, double mergeDays)
    {
        rewards = rewards.OrderBy(r => r.ReleaseTime).ToList();
        var mergedRewards = new List<RewardsMergeDto>();

        RewardsMergeDto currentMerge = null;

        foreach (var reward in rewards)
        {
            if (currentMerge == null)
            {
                currentMerge = new RewardsMergeDto
                {
                    ClaimedAmount = reward.ClaimedAmount,
                    ReleaseTime = reward.ReleaseTime,
                    ClaimIds = new List<string> { reward.ClaimId }
                };
            }
            else
            {
                var currentReleaseDate = DateTimeOffset.FromUnixTimeMilliseconds(currentMerge.ReleaseTime);
                var rewardReleaseDate = DateTimeOffset.FromUnixTimeMilliseconds(reward.ReleaseTime);
                var daysDifference = (rewardReleaseDate - currentReleaseDate).TotalDays;

                if (daysDifference <= mergeDays)
                {
                    currentMerge.ClaimedAmount =
                        (long.Parse(currentMerge.ClaimedAmount) + long.Parse(reward.ClaimedAmount)).ToString();
                    currentMerge.ReleaseTime = reward.ReleaseTime;
                    currentMerge.ClaimIds.Add(reward.ClaimId);
                }
                else
                {
                    mergedRewards.Add(currentMerge);
                    currentMerge = new RewardsMergeDto
                    {
                        ClaimedAmount = reward.ClaimedAmount,
                        ReleaseTime = reward.ReleaseTime,
                        ClaimIds = new List<string> { reward.ClaimId }
                    };
                }
            }
        }

        if (currentMerge != null)
        {
            mergedRewards.Add(currentMerge);
        }

        return mergedRewards;
    }

    public static (RewardsMergeDto Now, RewardsMergeDto NextReward) GetNextReward(List<RewardsMergeDto> rewards,
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

            next.ClaimIds = nClaimIds;
        }

        now.ClaimedAmount = withdrawableAmount.ToString();
        now.ClaimIds = wClaimIds;
        return (now, next);
    }


    private async Task<List<RewardsListIndexerDto>> GetAllRewardsList(string address, PoolTypeEnums poolType,
        List<string> liquidityIds = null)
    {
        var res = new List<RewardsListIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<RewardsListIndexerDto> list;
        do
        {
            var rewardsListIndexerResult = await _rewardsProvider.GetRewardsListAsync(poolType, address,
                skipCount, maxResultCount, liquidityIds: liquidityIds);
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