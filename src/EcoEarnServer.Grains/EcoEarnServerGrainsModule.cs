using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace EcoEarnServer.Grains;

[DependsOn(
    typeof(AbpAutoMapperModule), typeof(EcoEarnServerApplicationContractsModule))]
public class EcoEarnServerGrainsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<EcoEarnServerGrainsModule>(); });
    }
}