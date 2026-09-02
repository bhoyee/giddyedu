namespace GiddyEdu.Modules.Subscriptions;

public static class FeatureKeys
{
    public const string SchoolAdministration = "school-administration";
    public const string AcademicStructure = "academic-structure";
    public const string StaffManagement = "staff-management";
    public const string Admissions = "admissions";
    public const string StudentInformation = "student-information";
    public const string GuardianManagement = "guardian-management";

    public static readonly IReadOnlyCollection<string> PhaseOne =
    [
        SchoolAdministration, AcademicStructure, StaffManagement,
        Admissions, StudentInformation, GuardianManagement
    ];
}
