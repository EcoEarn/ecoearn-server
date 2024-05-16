using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface IUpdatePoolStakeSumProvider
{
}

public class UpdatePoolStakeSumProvider : IUpdatePoolStakeSumProvider, ISingletonDependency
{
}