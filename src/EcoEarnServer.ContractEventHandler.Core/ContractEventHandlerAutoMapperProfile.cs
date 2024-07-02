using AutoMapper;
using EcoEarnServer.Entities;
using EcoEarnServer.Users.Eto;

namespace EcoEarnServer.ContractEventHandler
{
    public class ContractEventHandlerAutoMapperProfile : Profile
    {
        public ContractEventHandlerAutoMapperProfile()
        {
            CreateMap<UserInformationEto, UserIndex>();
        }
    }
}