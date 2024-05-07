using EcoEarnServer.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace EcoEarnServer.Controllers;

public abstract class EcoEarnServerController : AbpControllerBase
{
    protected EcoEarnServerController()
    {
        LocalizationResource = typeof(EcoEarnServerResource);
    }
}