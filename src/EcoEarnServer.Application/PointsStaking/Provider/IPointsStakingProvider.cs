using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Common;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.ExceptionHandle;
using EcoEarnServer.PointsPool;
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.PointsStakeRewards;
using EcoEarnServer.Rewards.Provider;
using GraphQL;
using Microsoft.Extensions.Logging;
using Nest;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.PointsStaking.Provider;

public interface IPointsStakingProvider
{
    Task<long> GetProjectItemAggDataAsync(string snapshotDate, string dappId);
    Task<List<PointsPoolsIndexerDto>> GetPointsPoolsAsync(string name, string dappId = "", List<string> poolIds = null);

    Task<Dictionary<string, string>> GetPointsPoolStakeSumDicAsync(List<string> poolIds);
    Task<Dictionary<string, string>> GetAddressStakeAmountDicAsync(string address, string dappId = "");
    Task<Dictionary<string, string>> GetAddressStakeRewardsDicAsync(string address, string dappId = "");
    Task<List<RewardsListIndexerDto>> GetRealClaimInfoListAsync(List<string> seeds, string address, string poolId);
    Task<List<PointsPoolClaimRecordIndex>> GetClaimingListAsync(string address, string poolId);

    Task<List<PointsStakeRewardsSumIndex>> GetAddressRewardsAsync(string address, string dappId, int skipCount,
        int maxResultCount);

    Task<List<PointsPoolStakeSumIndex>> GetPointsPoolStakeSumAsync();
}

public class PointsStakingProvider : IPointsStakingProvider, ISingletonDependency
{
    private readonly INESTRepository<PointsSnapshotIndex, string> _pointsSnapshotRepository;
    private readonly INESTRepository<PointsPoolStakeSumIndex, string> _poolStakeSumRepository;
    private readonly INESTRepository<PointsPoolAddressStakeIndex, string> _addressStakeSumRepository;
    private readonly INESTRepository<PointsStakeRewardsSumIndex, string> _addressStakeRewardsRepository;
    private readonly INESTRepository<PointsPoolStakeSumIndex, string> _stakeSumRepository;
    private readonly INESTRepository<PointsPoolClaimRecordIndex, string> _claimRecordRepository;
    private readonly IObjectMapper _objectMapper;
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<PointsStakingProvider> _logger;


    public PointsStakingProvider(INESTRepository<PointsSnapshotIndex, string> pointsSnapshotRepository,
        IObjectMapper objectMapper, IGraphQlHelper graphQlHelper, ILogger<PointsStakingProvider> logger,
        INESTRepository<PointsPoolStakeSumIndex, string> poolStakeSumRepository,
        INESTRepository<PointsPoolAddressStakeIndex, string> addressStakeSumRepository,
        INESTRepository<PointsStakeRewardsSumIndex, string> addressStakeRewardsRepository,
        INESTRepository<PointsPoolClaimRecordIndex, string> claimRecordRepository,
        INESTRepository<PointsPoolStakeSumIndex, string> stakeSumRepository)
    {
        _pointsSnapshotRepository = pointsSnapshotRepository;
        _objectMapper = objectMapper;
        _graphQlHelper = graphQlHelper;
        _logger = logger;
        _poolStakeSumRepository = poolStakeSumRepository;
        _addressStakeSumRepository = addressStakeSumRepository;
        _addressStakeRewardsRepository = addressStakeRewardsRepository;
        _claimRecordRepository = claimRecordRepository;
        _stakeSumRepository = stakeSumRepository;
    }

    public async Task<long> GetProjectItemAggDataAsync(string snapshotDate, string dappId)
    {
        var list = await GetAllStakeInfoAsync(dappId);
        return list.Select(x => x.Address).Distinct().Count();
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService),
        ReturnDefault = ReturnDefault.New, MethodName = nameof(ExceptionHandlingService.HandleException),
        Message = "GetPointsPools Indexer error")]
    public async Task<List<PointsPoolsIndexerDto>> GetPointsPoolsAsync(string name, string dappId = "",
        List<string> poolIds = null)
    {
        var indexerResult = await _graphQlHelper.QueryAsync<PointsPoolsQuery>(new GraphQLRequest
        {
            Query =
                @"query($name:String!, $dappId:String!, $poolIds:[String!]!, $skipCount:Int!,$maxResultCount:Int!){
                    getPointsPoolList(input: {name:$name,dappId:$dappId,poolIds:$poolIds,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        dappId,
                        poolId,
                        pointsName,
                        amount,
    					createTime,
    					pointsPoolConfig{rewardToken,startBlockNumber,endBlockNumber,rewardPerBlock,releasePeriod,updateAddress,releasePeriods,claimInterval}
                    }
                }
            }",
            Variables = new
            {
                name = string.IsNullOrEmpty(name) ? "" : name, dappId = dappId,
                poolIds = poolIds ?? new List<string>(),
                skipCount = 0, maxResultCount = 5000
            }
        });

        return indexerResult.GetPointsPoolList.Data;
    }

    public async Task<Dictionary<string, string>> GetPointsPoolStakeSumDicAsync(List<string> poolIds)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<PointsPoolStakeSumIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Terms(i => i.Field(f => f.PoolId).Terms(poolIds)));

        QueryContainer Filter(QueryContainerDescriptor<PointsPoolStakeSumIndex> f) => f.Bool(b => b.Must(mustQuery));
        var (total, list) = await _poolStakeSumRepository.GetListAsync(Filter,
            skip: 0, limit: 5000);
        _logger.LogInformation("GetPointsPoolStakeSumDicAsync: {total}", total);
        return list.ToDictionary(x => x.PoolId, x => string.IsNullOrEmpty(x.StakeAmount) ? "0" : x.StakeAmount);
    }

    public async Task<Dictionary<string, string>> GetAddressStakeAmountDicAsync(string address, string dappId = "")
    {
        if (string.IsNullOrEmpty(address))
        {
            return new Dictionary<string, string>();
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<PointsPoolAddressStakeIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.Address).Value(address)));
        if (!string.IsNullOrEmpty(dappId))
        {
            mustQuery.Add(q => q.Term(i => i.Field(f => f.DappId).Value(dappId)));
        }

        QueryContainer Filter(QueryContainerDescriptor<PointsPoolAddressStakeIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (total, list) = await _addressStakeSumRepository.GetListAsync(Filter,
            skip: 0, limit: 5000);
        _logger.LogInformation("GetAddressStakeAmountDicAsync: {total}", total);
        return list.ToDictionary(x => GuidHelper.GenerateId(x.Address, x.PoolId), x => x.StakeAmount);
    }

    public async Task<Dictionary<string, string>> GetAddressStakeRewardsDicAsync(string address, string dappId = "")
    {
        if (string.IsNullOrEmpty(address))
        {
            return new Dictionary<string, string>();
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<PointsStakeRewardsSumIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.Address).Value(address)));
        if (!string.IsNullOrEmpty(dappId))
        {
            mustQuery.Add(q => q.Term(i => i.Field(f => f.DappId).Value(dappId)));
        }

        QueryContainer Filter(QueryContainerDescriptor<PointsStakeRewardsSumIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (total, list) = await _addressStakeRewardsRepository.GetListAsync(Filter,
            skip: 0, limit: 5000);
        _logger.LogInformation("GetAddressStakeRewardsDicAsync: {total}", total);
        return list.ToDictionary(x => GuidHelper.GenerateId(x.Address, x.PoolId),
            x => string.IsNullOrEmpty(x.Rewards) ? "0" : x.Rewards);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService),
        ReturnDefault = ReturnDefault.New, MethodName = nameof(ExceptionHandlingService.HandleException),
        Message = "GetRealClaimInfoList Indexer error")]
    public async Task<List<RewardsListIndexerDto>> GetRealClaimInfoListAsync(List<string> seeds, string address,
        string poolId)
    {
        if (string.IsNullOrEmpty(address) || seeds.IsNullOrEmpty())
        {
            return new List<RewardsListIndexerDto>();
        }

        var indexerResult = await _graphQlHelper.QueryAsync<RealRewardsListQuery>(new GraphQLRequest
        {
            Query =
                @"query($seeds:[String!]!, $address:String!, $poolId:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getRealClaimInfoList(input: {seeds:$seeds,address:$address,poolId:$poolId,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        claimId,
                        poolId,
                        claimedAmount,
                        seed,
                        claimedSymbol,
                        claimedBlockNumber,
                        earlyStakedAmount,
    					claimedTime,
    					releaseTime,
    					withdrawTime,
    					earlyStakeTime,
    					account,
    					poolType,
    					lockState,
    					withdrawSeed,
    					contractAddress,
                        liquidityAddedInfos{liquidityAddedSeed,liquidityId,tokenALossAmount,tokenBLossAmount,addedTime},
                        earlyStakeInfos{earlyStakeSeed,stakeId,stakeTime},
                    }
                }
            }",
            Variables = new
            {
                seeds = seeds, address = address, poolId = poolId, skipCount = 0, maxResultCount = 5000,
            }
        });

        return indexerResult.GetRealClaimInfoList.Data;
    }

    public async Task<List<PointsPoolClaimRecordIndex>> GetClaimingListAsync(string address, string poolId)
    {
        if (string.IsNullOrEmpty(address))
        {
            return new List<PointsPoolClaimRecordIndex>();
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<PointsPoolClaimRecordIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.Address).Value(address)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.PoolId).Value(poolId)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.ClaimStatus).Value(ClaimStatus.Claiming)));

        QueryContainer Filter(QueryContainerDescriptor<PointsPoolClaimRecordIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (total, list) = await _claimRecordRepository.GetListAsync(Filter, skip: 0, limit: 5000);

        return list;
    }

    public async Task<List<PointsStakeRewardsSumIndex>> GetAddressRewardsAsync(string address, string dappId,
        int skipCount, int maxResultCount)
    {
        if (string.IsNullOrEmpty(address))
        {
            return new List<PointsStakeRewardsSumIndex>();
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<PointsStakeRewardsSumIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.Address).Value(address)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.DappId).Value(dappId)));

        QueryContainer Filter(QueryContainerDescriptor<PointsStakeRewardsSumIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (total, list) = await _addressStakeRewardsRepository.GetListAsync(Filter, skip: skipCount,
            limit: maxResultCount, sortType: SortOrder.Descending, sortExp: o => o.CreateTime);

        return list;
    }

    public async Task<List<PointsPoolStakeSumIndex>> GetPointsPoolStakeSumAsync()
    {
        var (total, list) = await _stakeSumRepository.GetListAsync();
        return list;
    }

    private async Task<List<PointsPoolAddressStakeIndex>> GetAllStakeInfoAsync(string dappId)
    {
        var res = new List<PointsPoolAddressStakeIndex>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<PointsPoolAddressStakeIndex> list;
        do
        {
            var mustQuery = new List<Func<QueryContainerDescriptor<PointsPoolAddressStakeIndex>, QueryContainer>>();

            mustQuery.Add(q => q.Term(i => i.Field(f => f.DappId).Value(dappId)));

            QueryContainer Filter(QueryContainerDescriptor<PointsPoolAddressStakeIndex> f) =>
                f.Bool(b => b.Must(mustQuery));

            var result = await _addressStakeSumRepository.GetListAsync(Filter, skip: skipCount, limit: maxResultCount);

            list = result.Item2;
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