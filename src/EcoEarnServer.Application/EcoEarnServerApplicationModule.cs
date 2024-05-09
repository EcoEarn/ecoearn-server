using EcoEarnServer.Grains;
using EcoEarnServer.TokenStaking;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Account;
using Volo.Abp.AutoMapper;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace EcoEarnServer;

[DependsOn(
    typeof(EcoEarnServerDomainModule),
    typeof(AbpAccountApplicationModule),
    typeof(EcoEarnServerApplicationContractsModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(EcoEarnServerGrainsModule),
    typeof(AbpSettingManagementApplicationModule)
)]
public class EcoEarnServerApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<EcoEarnServerApplicationModule>(); });
        context.Services.AddSingleton<ITokenStakingService, TokenStakingService>();
        context.Services.AddHttpClient();
    }
}