using Xunit.Abstractions;

namespace EcoEarnServer;

public abstract partial class
    EcoEarnServerApplicationTestBase : EcoEarnServerOrleansTestBase<EcoEarnServerApplicationTestModule>
{
    public readonly ITestOutputHelper Output;

    protected EcoEarnServerApplicationTestBase(ITestOutputHelper output) : base(output)
    {
        Output = output;
    }
}