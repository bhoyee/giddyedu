namespace GiddyEdu.Modules.Identity;

public static class Permissions
{
    public const string RolesManage = "Roles.Manage";
    public const string UsersManage = "Users.Manage";
    public const string TenantSettingsManage = "TenantSettings.Manage";
    public const string CustomFieldsManage = "CustomFields.Manage";
    public const string FilesManage = "Files.Manage";

    public static readonly IReadOnlyCollection<string> Foundation =
    [
        RolesManage, UsersManage, TenantSettingsManage, CustomFieldsManage, FilesManage
    ];
}
