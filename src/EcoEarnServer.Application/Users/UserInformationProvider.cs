using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using EcoEarnServer.Grains.Grain.Users;
using EcoEarnServer.Users.Eto;
using Nest;
using Orleans;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer.Users;

public interface IUserInformationProvider
{
    Task<bool> CheckNameAsync(string name, Guid? userId);

    Task<UserGrainDto> GetUserById(Guid id);

    Task<UserGrainDto> SaveUserSourceAsync(UserGrainDto userSourceInput);

    Task<UserIndex> GetByUserAddressAsync(string inputAddress);
    Task<long> GetUserCount();
}

public class UserInformationProvider : IUserInformationProvider, ISingletonDependency

{
    private readonly INESTRepository<UserIndex, Guid> _userIndexRepository;
    private readonly INESTRepository<UserExtraIndex, Guid> _userExtraIndexRepository;
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    private readonly IDistributedEventBus _distributedEventBus;

    public UserInformationProvider(INESTRepository<UserIndex, Guid> userIndexRepository,
        INESTRepository<UserExtraIndex, Guid> userExtraIndexRepository,
        IClusterClient clusterClient, IObjectMapper objectMapper, IDistributedEventBus distributedEventBus)
    {
        _userIndexRepository = userIndexRepository;
        _userExtraIndexRepository = userExtraIndexRepository;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _distributedEventBus = distributedEventBus;
    }

    public async Task<bool> CheckNameAsync(string name, Guid? userId)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<UserIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Terms(i => i.Field(f => f.Name).Terms(name)));
        QueryContainer Filter(QueryContainerDescriptor<UserIndex> f) => f.Bool(b => b.Must(mustQuery));
        var countResponse = await _userIndexRepository.GetAsync(Filter);
        return countResponse == null || countResponse.Id == userId;
    }

    public async Task<UserGrainDto> GetUserById(Guid id)
    {
        var userGrain = _clusterClient.GetGrain<IUserGrain>(id);
        var resp = await userGrain.GetUserAsync();
        return resp.Success ? resp.Data : null;
    }

    public async Task<UserGrainDto> SaveUserSourceAsync(UserGrainDto userSourceInput)
    {
        var userGrain = _clusterClient.GetGrain<IUserGrain>(userSourceInput.Id);
        var result = await userGrain.UpdateUserAsync(userSourceInput);
        await _distributedEventBus.PublishAsync(
            _objectMapper.Map<UserGrainDto, UserInformationEto>(result.Data));
        return result.Data;
    }

    public async Task<UserIndex> GetByUserAddressAsync(string inputAddress)
    {
        if (inputAddress.IsNullOrWhiteSpace())
        {
            return null;
        }

        var shouldQuery = new List<Func<QueryContainerDescriptor<UserIndex>, QueryContainer>>();
        shouldQuery.Add(q => q.Terms(i => i.Field(f => f.AelfAddress).Terms(inputAddress)));
        shouldQuery.Add(q => q.Terms(i => i.Field(f => f.CaAddressMain).Terms(inputAddress)));
        shouldQuery.Add(q => q.Nested(i => i.Path("CaAddressListSide").Query(nq => nq
            .Terms(mm => mm
                .Field("CaAddressListSide.address")
                .Terms(inputAddress)
            )
        )));
        QueryContainer Filter(QueryContainerDescriptor<UserIndex> f) => f.Bool(b => b.Should(shouldQuery));

        return await _userIndexRepository.GetAsync(Filter);
    }

    public async Task<long> GetUserCount()
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<UserIndex>, QueryContainer>>();
        QueryContainer Filter(QueryContainerDescriptor<UserIndex> f) => f.Bool(b => b.Must(mustQuery));
        var countResponse = await _userIndexRepository.CountAsync(Filter);
        return countResponse.Count;
    }
}