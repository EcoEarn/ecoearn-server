using EcoEarnServer.EntityEventHandler.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Volo.Abp.AuditLogging;
using Volo.Abp.AuditLogging.MongoDB;
using Volo.Abp.AutoMapper;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.Identity.MongoDB;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace EcoEarnServer;

[DependsOn(
    typeof(AbpEventBusModule),
    typeof(EcoEarnServerApplicationModule),
    typeof(EcoEarnServerApplicationContractsModule),
    typeof(EcoEarnServerOrleansTestBaseModule),
    typeof(EcoEarnServerDomainTestModule)
)]
public class EcoEarnServerApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        base.ConfigureServices(context);
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<EcoEarnServerApplicationModule>(); });
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<EcoEarnServerEntityEventHandlerCoreModule>(); });

        context.Services.AddSingleton(new Mock<IMongoDbContextProvider<IAuditLoggingMongoDbContext>>().Object);
        context.Services.AddSingleton<IAuditLogRepository, MongoAuditLogRepository>();
        context.Services.AddSingleton<IIdentityUserRepository, MongoIdentityUserRepository>();

    }
}