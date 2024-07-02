using System.Threading.Tasks;

namespace EcoEarnServer.Data;

public interface IEcoEarnServerDbSchemaMigrator
{
    Task MigrateAsync();
}