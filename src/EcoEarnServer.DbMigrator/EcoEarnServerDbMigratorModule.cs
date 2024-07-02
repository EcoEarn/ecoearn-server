using EcoEarnServer.MongoDB;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Modularity;

namespace EcoEarnServer.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(EcoEarnServerMongoDbModule),
    typeof(EcoEarnServerApplicationContractsModule)
)]
public class EcoEarnServerDbMigratorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
    }
}