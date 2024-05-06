using EcoEarnServer.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace EcoEarnServer.Controllers;

public class EcoEarnServerController : AbpControllerBase
{
    protected EcoEarnServerController()
    {
        LocalizationResource = typeof(EcoEarnServerResource);
    }
}