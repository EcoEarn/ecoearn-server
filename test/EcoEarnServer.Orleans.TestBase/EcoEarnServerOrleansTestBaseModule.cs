using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.Caching;
using Volo.Abp.Modularity;
using Volo.Abp.ObjectMapping;

namespace EcoEarnServer;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpCachingModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpObjectMappingModule),
    typeof(EcoEarnServerDomainModule)
)]
public class EcoEarnServerOrleansTestBaseModule : AbpModule
{
    private readonly ClusterFixture _fixture = new();

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton(_fixture);
        context.Services.AddSingleton<IClusterClient>(sp => _fixture.Cluster.Client);
    }
}