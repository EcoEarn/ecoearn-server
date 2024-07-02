using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace EcoEarnServer.EntityEventHandler.Core
{
    [DependsOn(typeof(AbpAutoMapperModule),
        typeof(EcoEarnServerApplicationModule),
        typeof(EcoEarnServerApplicationContractsModule))]
    public class EcoEarnServerEntityEventHandlerCoreModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpAutoMapperOptions>(options =>
            {
                options.AddMaps<EcoEarnServerEntityEventHandlerCoreModule>();
            });
        }
    }
}