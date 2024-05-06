using AutoMapper;
using EcoEarnServer.Entities;
using EcoEarnServer.Users;

namespace EcoEarnServer.EntityEventHandler.Core;

public class EcoEarnServerEventHandlerAutoMapperProfile : Profile
{
    public EcoEarnServerEventHandlerAutoMapperProfile()
    {
        CreateMap<UserGrainDto, UserIndex>();
    }
}