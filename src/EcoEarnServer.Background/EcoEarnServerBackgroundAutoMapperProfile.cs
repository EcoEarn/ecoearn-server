using System.Globalization;
using AutoMapper;
using EcoEarnServer.Background.Dtos;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Background.Services.Dtos;
using EcoEarnServer.Common;
using EcoEarnServer.Grains.Grain.PointsPool;
using EcoEarnServer.Grains.Grain.PointsSnapshot;
using EcoEarnServer.Grains.Grain.PointsStakeRewards;
using EcoEarnServer.Grains.Grain.StakingPoints;
using EcoEarnServer.PointsPool;
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.PointsStakeRewards;
using EcoEarnServer.StakingSettlePoints;

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
        CreateMap<PointsListDto, PointsSnapshotEto>().ReverseMap();
        CreateMap<AddressStakingPointsDto, AddressStakingSettlePointsDto>()
            .ForMember(t => t.Points, m => m.MapFrom(f => f.Points.ToString(CultureInfo.InvariantCulture)))
            .ForMember(t => t.Id, m => m.MapFrom(f => GuidHelper.GenerateId(f.Address)))
            .ReverseMap();
        CreateMap<StakingPointsDto, StakingSettlePointsDto>()
            .ForMember(t => t.Points, m => m.MapFrom(f => f.Points.ToString(CultureInfo.InvariantCulture)))
            .ReverseMap();
        CreateMap<AddressStakingSettlePointsDto, AddressStakingSettlePointsEto>().ReverseMap();
    }
}