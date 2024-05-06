using AutoMapper;
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
    }
}