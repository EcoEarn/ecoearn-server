using AutoMapper;
using EcoEarnServer.Background.Dtos;
using EcoEarnServer.Grains.Grain.PointsSnapshot;
using EcoEarnServer.PointsSnapshot;

namespace EcoEarnServer.Background;

public class EcoEarnServerBackgroundAutoMapperProfile : Profile
{
    public EcoEarnServerBackgroundAutoMapperProfile()
    {
        CreateMap<PointsListDto, PointsSnapshotDto>().ReverseMap();
        CreateMap<PointsSnapshotDto, PointsSnapshotEto>().ReverseMap();
    }
}