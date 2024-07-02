using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Services;

public interface IMetricsService
{
    Task GenerateMetricsAsync();
}

public class MetricsService : IMetricsService, ISingletonDependency
{
    public Task GenerateMetricsAsync()
    {
        throw new System.NotImplementedException();
    }
    
    
}