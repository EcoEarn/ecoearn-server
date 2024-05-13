using AutoMapper;
using EcoEarnServer.Constants;
using EcoEarnServer.Grains.Grain.PointsStakeRewards;
using EcoEarnServer.Options;
using EcoEarnServer.PointsStakeRewards;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.PointsStaking.Provider;
using EcoEarnServer.Rewards.Dtos;
using EcoEarnServer.Rewards.Provider;
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
            .ForPath(t => t.PoolName, m => m.MapFrom(f => f.TokenPoolConfig.StakingToken))
            .ForPath(t => t.EarnedSymbol, m => m.MapFrom(f => f.TokenPoolConfig.RewardToken))
            .ForPath(t => t.StakeSymbol, m => m.MapFrom(f => f.TokenPoolConfig.StakingToken))
            .ForPath(t => t.ProjectOwner, m => m.MapFrom(f => PoolInfoConst.ProjectOwnerDic[f.DappId]))
            .ReverseMap();

        CreateMap<RewardsListIndexerDto, RewardsListDto>().ReverseMap();
        CreateMap<PointsStakeRewardsSumDto, PointsStakeRewardsSumEto>().ReverseMap();
    }
}