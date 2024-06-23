using AutoMapper;
using EcoEarnServer.Farm.Dtos;
using EcoEarnServer.Farm.Provider;
using EcoEarnServer.Grains.Grain.PointsPool;
using EcoEarnServer.Grains.Grain.PointsStakeRewards;
using EcoEarnServer.Grains.Grain.Rewards;
using EcoEarnServer.Options;
using EcoEarnServer.PointsPool;
using EcoEarnServer.PointsStakeRewards;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.PointsStaking.Provider;
using EcoEarnServer.Rewards;
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
            .ForMember(t => t.PoolName, m => m.MapFrom(f => f.PointsName))
            .ForMember(t => t.StakeTokenName, m => m.MapFrom(f => f.PointsName))
            .ForPath(t => t.RewardsTokenName, m => m.MapFrom(f => f.PointsPoolConfig.RewardToken))
            .ForPath(t => t.PoolDailyRewards,
                m => m.MapFrom(f => (decimal)f.PointsPoolConfig.RewardPerBlock * 86400 / 100000000))
            .ForPath(t => t.ReleasePeriod, m => m.MapFrom(f => f.PointsPoolConfig.ReleasePeriod))
            ;


        CreateMap<TokenPoolsIndexerDto, TokenPoolsDto>()
            .ForMember(t => t.PoolId, m => m.MapFrom(f => f.PoolId))
            .ForPath(t => t.PoolName, m => m.MapFrom(f => f.TokenPoolConfig.StakingToken))
            .ForPath(t => t.EarnedSymbol, m => m.MapFrom(f => f.TokenPoolConfig.RewardToken))
            .ForPath(t => t.StakeSymbol, m => m.MapFrom(f => f.TokenPoolConfig.StakingToken))
            .ForPath(t => t.ProjectOwner,
                m => m.MapFrom(f => f.PoolType == PoolTypeEnums.Lp ? "AwakenSwap" : f.PoolType == PoolTypeEnums.Token ? "Schrödinger" : "AI-powered 404 NFT dApp"))
            .ForPath(t => t.FixedBoostFactor, m => m.MapFrom(f => f.TokenPoolConfig.FixedBoostFactor))
            .ForPath(t => t.UnlockWindowDuration, m => m.MapFrom(f => f.TokenPoolConfig.UnlockWindowDuration))
            .ForPath(t => t.MinimumClaimAmount, m => m.MapFrom(f => f.TokenPoolConfig.MinimumClaimAmount))
            .ForPath(t => t.ReleasePeriod, m => m.MapFrom(f => f.TokenPoolConfig.ReleasePeriod))
            .ReverseMap();

        CreateMap<RewardsListIndexerDto, RewardsListDto>()
            .ForPath(t => t.ProjectOwner, m => m.MapFrom(f => f.PoolType == PoolTypeEnums.Lp ? "AwakenSwap" : "Schrödinger"))
            .ForPath(t => t.RewardsToken, m => m.MapFrom(f => f.ClaimedSymbol))
            .ForPath(t => t.Rewards, m => m.MapFrom(f => f.ClaimedAmount))
            .ForPath(t => t.ClaimedId, m => m.MapFrom(f => f.ClaimId))
            .ForPath(t => t.TokenName, m => m.MapFrom(f => ""))
            .ForPath(t => t.Date, m => m.MapFrom(f => f.ClaimedTime))
            .ReverseMap();
        CreateMap<PointsStakeRewardsSumDto, PointsStakeRewardsSumEto>().ReverseMap();
        CreateMap<PointsPoolClaimRecordDto, PointsPoolClaimRecordEto>().ReverseMap();
        CreateMap<RewardOperationRecordDto, RewardOperationRecordEto>().ReverseMap();
        CreateMap<SubStakeInfoIndexerDto, SubStakeInfoDto>().ReverseMap();
        CreateMap<LiquidityInfoIndexerDto, LiquidityInfoDto>()
            .ForMember(t => t.Banlance, m => m.MapFrom(f => f.LpAmount.ToString()))
            .ForMember(t => t.TokenAAmount, m => m.MapFrom(f => f.TokenAAmount.ToString()))
            .ForMember(t => t.TokenBAmount, m => m.MapFrom(f => f.TokenBAmount.ToString()))
            .ReverseMap();
    }
}