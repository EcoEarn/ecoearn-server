using Orleans.TestingHost;
using Volo.Abp.Modularity;
using Xunit.Abstractions;

namespace EcoEarnServer;

public abstract class EcoEarnServerOrleansTestBase<TStartupModule> :
    EcoEarnServerTestBase<TStartupModule> where TStartupModule : IAbpModule
{
    protected readonly TestCluster Cluster;

    public EcoEarnServerOrleansTestBase(ITestOutputHelper output) : base(output)
    {
        Cluster = GetRequiredService<ClusterFixture>().Cluster;
    }
}