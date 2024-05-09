using AutoMapper;
using EcoEarnServer.Background.Dtos;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Grains.Grain.PointsPool;
using EcoEarnServer.Grains.Grain.PointsSnapshot;
using EcoEarnServer.Grains.Grain.PointsStakeRewards;
using EcoEarnServer.PointsPool;
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.PointsStakeRewards;

namespace EcoEarnServer.Background;

public class EcoEarnServerBackgroundAutoMapperProfile : Profile
{
    public EcoEarnServerBackgroundAutoMapperProfile()
    {
        CreateMap<PointsListDto, PointsSnapshotDto>().ReverseMap();
        CreateMap<PointsSnapshotDto, PointsSnapshotEto>().ReverseMap();
        CreateMap<PointsPoolInfo, PointsPoolStakeSumDto>().ReverseMap();
        CreateMap<PointsPoolAddressStakeDto, PointsPoolAddressStakeEto>().ReverseMap();
        CreateMap<PointsStakeRewardsDto, PointsStakeRewardsEto>().ReverseMap();
        CreateMap<PointsStakeRewardsSumDto, PointsStakeRewardsSumEto>().ReverseMap();
        CreateMap<PointsPoolStakeSumDto, PointsPoolStakeSumEto>().ReverseMap();
    }
}