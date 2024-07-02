using EcoEarnServer.Grains;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace EcoEarnServer.Silo;

[DependsOn(typeof(AbpAutofacModule),
    typeof(EcoEarnServerGrainsModule),
    typeof(AbpAspNetCoreSerilogModule)
)]
public class EcoEarnServerOrleansSiloModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHostedService<EcoEarnServerHostedService>();
    }
}