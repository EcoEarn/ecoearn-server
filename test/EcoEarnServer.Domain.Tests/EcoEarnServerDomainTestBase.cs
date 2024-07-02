using Xunit.Abstractions;

namespace EcoEarnServer;

public abstract class EcoEarnServerDomainTestBase : EcoEarnServerTestBase<EcoEarnServerDomainTestModule>
{
    protected EcoEarnServerDomainTestBase(ITestOutputHelper output) : base(output)
    {
    }
}