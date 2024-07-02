using EcoEarnServer.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace EcoEarnServer.Permissions;

public class EcoEarnServerPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(EcoEarnServerPermissions.GroupName);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(EcoEarnServerPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<EcoEarnServerResource>(name);
    }
}