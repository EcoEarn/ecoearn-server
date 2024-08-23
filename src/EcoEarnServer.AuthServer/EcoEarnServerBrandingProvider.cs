using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace EcoEarnServer;

[Dependency(ReplaceServices = true)]
public class EcoEarnServerBrandingProvider : DefaultBrandingProvider
{
    public override string AppName => "EcoEarnServer";
}