using AutoMapper;
using EcoEarnServer.Options;
using EcoEarnServer.PointsStaking.Dtos;
using EcoEarnServer.PointsStaking.Provider;
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
    }
}