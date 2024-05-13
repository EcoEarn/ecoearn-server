using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Common;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.PointsPool;
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.PointsStakeRewards;
using GraphQL;
using Microsoft.Extensions.Logging;
using Nest;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.PointsStaking.Provider;

public interface IPointsStakingProvider
{
    Task<List<PointsSnapshotIndex>> GetProjectItemAggDataAsync(string snapshotDate, int skipCount, int maxResultCount);
    Task<List<PointsPoolsIndexerDto>> GetPointsPoolsAsync(string name, List<string> poolIds = null);

    Task<Dictionary<string, string>> GetPointsPoolStakeSumDicAsync(List<string> poolIds);
    Task<Dictionary<string, string>> GetAddressStakeAmountDicAsync(string address);
    Task<Dictionary<string, string>> GetAddressStakeRewardsDicAsync(string address);
}

public class PointsStakingProvider : IPointsStakingProvider, ISingletonDependency
{
    private readonly INESTRepository<PointsSnapshotIndex, string> _pointsSnapshotRepository;
    private readonly INESTRepository<PointsPoolStakeSumIndex, string> _poolStakeSumRepository;
    private readonly INESTRepository<PointsPoolAddressStakeIndex, string> _addressStakeSumRepository;
    private readonly INESTRepository<PointsStakeRewardsSumIndex, string> _addressStakeRewardsRepository;
    private readonly IObjectMapper _objectMapper;
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<PointsStakingProvider> _logger;


    public PointsStakingProvider(INESTRepository<PointsSnapshotIndex, string> pointsSnapshotRepository,
        IObjectMapper objectMapper, IGraphQlHelper graphQlHelper, ILogger<PointsStakingProvider> logger,
        INESTRepository<PointsPoolStakeSumIndex, string> poolStakeSumRepository,
        INESTRepository<PointsPoolAddressStakeIndex, string> addressStakeSumRepository,
        INESTRepository<PointsStakeRewardsSumIndex, string> addressStakeRewardsRepository)
    {
        _pointsSnapshotRepository = pointsSnapshotRepository;
        _objectMapper = objectMapper;
        _graphQlHelper = graphQlHelper;
        _logger = logger;
        _poolStakeSumRepository = poolStakeSumRepository;
        _addressStakeSumRepository = addressStakeSumRepository;
        _addressStakeRewardsRepository = addressStakeRewardsRepository;
    }

    public async Task<List<PointsSnapshotIndex>> GetProjectItemAggDataAsync(string snapshotDate, int skipCount,
        int maxResultCount)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<PointsSnapshotIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.SnapshotDate).Value(snapshotDate)));

        QueryContainer Filter(QueryContainerDescriptor<PointsSnapshotIndex> f) => f.Bool(b => b.Must(mustQuery));
        var (total, list) = await _pointsSnapshotRepository.GetListAsync(Filter,
            skip: skipCount, limit: maxResultCount);
        return list;
    }

    public async Task<List<PointsPoolsIndexerDto>> GetPointsPoolsAsync(string name, List<string> poolIds = null)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<PointsPoolsQuery>(new GraphQLRequest
            {
                Query =
                    @"query($name:String!, $poolIds:[String!]!, $skipCount:Int!,$maxResultCount:Int!){
                    getPointsPoolList(input: {name:$name,poolIds:$poolIds,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        dappId,
                        poolId,
                        pointsName,
                        amount,
    					createTime,
    					pointsPoolConfig{rewardToken,startBlockNumber,endBlockNumber,rewardPerBlock,releasePeriod,updateAddress}
                    }
                }
            }",
                Variables = new
                {
                    name = string.IsNullOrEmpty(name) ? "" : name, poolIds = poolIds, skipCount = 0, maxResultCount = 5000
                }
            });

            return indexerResult.GetPointsPoolList.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetPointsPools Indexer error");
            return new List<PointsPoolsIndexerDto>();
        }
    }

    public async Task<Dictionary<string, string>> GetPointsPoolStakeSumDicAsync(List<string> poolIds)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<PointsPoolStakeSumIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Terms(i => i.Field(f => f.PoolId).Terms(poolIds)));

        QueryContainer Filter(QueryContainerDescriptor<PointsPoolStakeSumIndex> f) => f.Bool(b => b.Must(mustQuery));
        var (total, list) = await _poolStakeSumRepository.GetListAsync(Filter,
            skip: 0, limit: 5000);
        _logger.LogInformation("GetPointsPoolStakeSumDicAsync: {total}", total);
        return list.ToDictionary(x => x.PoolId, x => x.StakeAmount);
    }

    public async Task<Dictionary<string, string>> GetAddressStakeAmountDicAsync(string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<PointsPoolAddressStakeIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.Address).Value(address)));

        QueryContainer Filter(QueryContainerDescriptor<PointsPoolAddressStakeIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (total, list) = await _addressStakeSumRepository.GetListAsync(Filter,
            skip: 0, limit: 5000);
        _logger.LogInformation("GetAddressStakeAmountDicAsync: {total}", total);
        return list.ToDictionary(x => GuidHelper.GenerateId(x.Address, x.PoolId), x => x.StakeAmount);
    }

    public async Task<Dictionary<string, string>> GetAddressStakeRewardsDicAsync(string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<PointsStakeRewardsSumIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(f => f.Address).Value(address)));

        QueryContainer Filter(QueryContainerDescriptor<PointsStakeRewardsSumIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (total, list) = await _addressStakeRewardsRepository.GetListAsync(Filter,
            skip: 0, limit: 5000);
        _logger.LogInformation("GetAddressStakeRewardsDicAsync: {total}", total);
        return list.ToDictionary(x => GuidHelper.GenerateId(x.Address, x.PoolId), x => x.Rewards);
    }
}