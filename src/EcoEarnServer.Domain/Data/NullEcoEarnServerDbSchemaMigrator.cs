using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Data;

/* This is used if database provider does't define
 * IEcoEarnServerDbSchemaMigrator implementation.
 */
public class NullEcoEarnServerDbSchemaMigrator : IEcoEarnServerDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}