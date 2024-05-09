using AutoMapper;
using EcoEarnServer.Constants;
using EcoEarnServer.Options;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.PointsStaking.Provider;
using EcoEarnServer.TokenStaking.Dtos;
using EcoEarnServer.TokenStaking.Provider;
using EcoEarnServer.Users;
using EcoEarnServer.Users.Eto;

namespace EcoEarnServer;

public class EcoEarnServerApplicationAutoMapperProfile : Profile
{
    public EcoEarnServerApplicationAutoMapperProfile()
    {
        CreateMap<UserGrainDto, UserInformationEto>().ReverseMap();
        CreateMap<ProjectItem, ProjectItemListDto>().ReverseMap();
        CreateMap<PointsPoolsIndexerDto, PointsPoolsDto>()
            .ForMember(t => t.PoolName, m => m.MapFrom(f => f.PointsName));
    
        CreateMap<TokenPoolsIndexerDto, TokenPoolsDto>()
            .ForMember(t => t.PoolId, m => m.MapFrom(f => f.PoolId))
            .ForMember(t => t.PoolName, m => m.MapFrom(f => f.TokenPoolConfig.StakingToken))
            .ForMember(t => t.EarnedSymbol, m => m.MapFrom(f => f.TokenPoolConfig.RewardToken))
            .ForMember(t => t.StakeSymbol, m => m.MapFrom(f => f.TokenPoolConfig.StakingToken))
            .ForMember(t => t.ProjectOwner, m => m.MapFrom(f => PoolInfoConst.ProjectOwnerDic[f.DappId]))
            ;
        CreateMap<TokenPoolsIndexerDto, TokenPoolsDto>().ReverseMap();

    }
}