namespace GiddyEdu.Modules.Identity;

public static class Permissions
{
    public const string RolesManage = "Roles.Manage";
    public const string UsersManage = "Users.Manage";
    public const string TenantSettingsManage = "TenantSettings.Manage";
    public const string CustomFieldsManage = "CustomFields.Manage";
    public const string FilesManage = "Files.Manage";
    public const string SchoolsView = "Schools.View";
    public const string SchoolsManage = "Schools.Manage";
    public const string AcademicsView = "Academics.View";
    public const string AcademicsManage = "Academics.Manage";
    public const string StaffView = "Staff.View";
    public const string StaffManage = "Staff.Manage";
    public const string StaffSensitiveView = "Staff.Sensitive.View";
    public const string AdmissionsView = "Admissions.View";
    public const string AdmissionsManage = "Admissions.Manage";
    public const string StudentsView = "Students.View";
    public const string StudentsManage = "Students.Manage";
    public const string GuardiansView = "Guardians.View";
    public const string GuardiansManage = "Guardians.Manage";
    public const string AdmissionsSensitiveView = "Admissions.Sensitive.View";
    public const string AdmissionsSensitiveManage = "Admissions.Sensitive.Manage";
    public const string StudentsSensitiveView = "Students.Sensitive.View";
    public const string StudentsSensitiveManage = "Students.Sensitive.Manage";

    public static readonly IReadOnlyCollection<string> Foundation =
    [
        RolesManage, UsersManage, TenantSettingsManage, CustomFieldsManage, FilesManage,
        SchoolsView, SchoolsManage, AcademicsView, AcademicsManage,
        StaffView, StaffManage, StaffSensitiveView,
        AdmissionsView, AdmissionsManage, StudentsView, StudentsManage, GuardiansView, GuardiansManage,
        AdmissionsSensitiveView, AdmissionsSensitiveManage, StudentsSensitiveView, StudentsSensitiveManage
    ];
}
