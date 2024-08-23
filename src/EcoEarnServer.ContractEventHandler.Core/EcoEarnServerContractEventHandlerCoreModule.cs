using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace EcoEarnServer.ContractEventHandler.Core
{
    [DependsOn(
        typeof(AbpAutoMapperModule)
    )]
    public class EcoEarnServerContractEventHandlerCoreModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpAutoMapperOptions>(options =>
            {
                options.AddMaps<EcoEarnServerContractEventHandlerCoreModule>();
            });
        }
    }
}