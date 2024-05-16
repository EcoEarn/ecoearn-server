using AutoMapper;
using EcoEarnServer.Grains.Grain.PointsPool;
using EcoEarnServer.Grains.Grain.PointsSnapshot;
using EcoEarnServer.Grains.Grain.PointsStakeRewards;
using EcoEarnServer.Grains.State;
using EcoEarnServer.Grains.State.Users;
using EcoEarnServer.Users;
using EcoEarnServer.Users.Eto;

namespace EcoEarnServer.Grains;

public class EcoEarnServerGrainsAutoMapperProfile : Profile
{
    public EcoEarnServerGrainsAutoMapperProfile()
    {
        CreateMap<UserGrainDto, UserState>().ReverseMap();
        CreateMap<UserGrainDto, UserInformationEto>().ReverseMap();
        CreateMap<PointsSnapshotDto, PointsSnapshotState>().ReverseMap();
        CreateMap<PointsPoolAddressStakeDto, PointsPoolAddressStakeState>().ReverseMap();
        CreateMap<PointsPoolStakeSumDto, PointsPoolStakeSumState>().ReverseMap();
        CreateMap<PointsStakeRewardsDto, PointsStakeRewardsState>().ReverseMap();
        CreateMap<PointsStakeRewardsSumDto, PointsStakeRewardsSumState>().ReverseMap();
        CreateMap<PointsPoolClaimRecordDto, PointsPoolClaimRecordState>().ReverseMap();
    }
}