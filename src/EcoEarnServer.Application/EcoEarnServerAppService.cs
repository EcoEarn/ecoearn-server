using EcoEarnServer.Localization;
using Volo.Abp.Application.Services;

namespace EcoEarnServer;

/* Inherit your application services from this class.
 */
public abstract class EcoEarnServerAppService : ApplicationService
{
    protected EcoEarnServerAppService()
    {
        LocalizationResource = typeof(EcoEarnServerResource);
    }
}