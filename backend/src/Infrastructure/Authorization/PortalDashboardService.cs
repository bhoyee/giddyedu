using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Subscriptions;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Authorization;

public sealed record PortalDashboardMetric(string Key, string Label, long Value, string? Href);
public sealed record PortalDashboard(string Audience, IReadOnlyList<PortalDashboardMetric> Metrics, string Guidance);
public sealed record TeachingClassSummary(Guid AssignmentId, Guid ClassSectionId, string ClassName, string ClassCode, string Responsibility, Guid? SubjectId, string? SubjectName, long? StudentCount);

public interface IPortalDashboardService
{
    Task<PortalDashboard> GetAsync(Guid userId, string audience, CancellationToken ct = default);
    Task<IReadOnlyList<TeachingClassSummary>> GetTeachingClassesAsync(Guid userId, CancellationToken ct = default);
}

public sealed class PortalDashboardService(
    GiddyEduDbContext db,
    IPermissionService permissions,
    IAccessProfileService profiles,
    IEntitlementService entitlements) : IPortalDashboardService
{
    public async Task<PortalDashboard> GetAsync(Guid userId, string audience, CancellationToken ct = default)
    {
        var effectivePermissions = await permissions.GetEffectivePermissionsAsync(userId, ct);
        var presentation = await profiles.GetPresentationAsync(userId, effectivePermissions, ct);
        var canonicalAudience = presentation.Audiences.SingleOrDefault(x => string.Equals(x, audience, StringComparison.OrdinalIgnoreCase));
        if (canonicalAudience is null) throw new UnauthorizedAccessException("The requested workspace is not available for this account.");

        return canonicalAudience switch
        {
            "SchoolAdmin" => await AdministrativeDashboardAsync(canonicalAudience, effectivePermissions, ct),
            "SuperAdmin" => await AdministrativeDashboardAsync(canonicalAudience, effectivePermissions, ct),
            "Teacher" => await StaffDashboardAsync(userId, canonicalAudience, true, effectivePermissions, ct),
            "Staff" => await StaffDashboardAsync(userId, canonicalAudience, false, effectivePermissions, ct),
            "Parent" => await ParentDashboardAsync(userId, effectivePermissions, ct),
            "Student" => await StudentDashboardAsync(userId, effectivePermissions, ct),
            "Accountant" => await AccountantDashboardAsync(effectivePermissions, ct),
            _ => throw new UnauthorizedAccessException("The requested workspace is not supported.")
        };
    }

    public async Task<IReadOnlyList<TeachingClassSummary>> GetTeachingClassesAsync(Guid userId, CancellationToken ct = default)
    {
        var effectivePermissions = await permissions.GetEffectivePermissionsAsync(userId, ct);
        var presentation = await profiles.GetPresentationAsync(userId, effectivePermissions, ct);
        if (!presentation.Audiences.Contains("Teacher") || !await CanUseAsync(effectivePermissions, Permissions.AcademicsView, FeatureKeys.AcademicStructure, ct))
            throw new UnauthorizedAccessException("The teacher workspace is not available for this account.");

        var staffId = await db.StaffProfiles.Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (!staffId.HasValue) return [];
        var mayViewStudents = await CanUseAsync(effectivePermissions, Permissions.StudentsView, FeatureKeys.StudentInformation, ct);
        var rows = await (from assignment in db.TeachingAssignments.AsNoTracking()
                          join section in db.ClassSections.AsNoTracking() on assignment.ClassSectionId equals section.Id
                          where assignment.StaffId == staffId.Value
                          orderby section.Name, assignment.Role
                          select new { assignment.Id, assignment.ClassSectionId, section.Name, section.Code, assignment.Role, assignment.SubjectId }).ToListAsync(ct);
        var subjectIds = rows.Where(x => x.SubjectId.HasValue).Select(x => x.SubjectId!.Value).Distinct().ToArray();
        var subjectNames = await db.Subjects.AsNoTracking().Where(x => subjectIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var classIds = rows.Select(x => x.ClassSectionId).Distinct().ToArray();
        var studentCounts = mayViewStudents
            ? await db.Enrollments.AsNoTracking().Where(x => classIds.Contains(x.ClassSectionId) && x.Status == EnrollmentStatus.Active)
                .GroupBy(x => x.ClassSectionId).Select(x => new { ClassSectionId = x.Key, Count = x.LongCount() })
                .ToDictionaryAsync(x => x.ClassSectionId, x => x.Count, ct)
            : [];
        return rows.Select(x => new TeachingClassSummary(x.Id, x.ClassSectionId, x.Name, x.Code, x.Role.ToString(), x.SubjectId,
            x.SubjectId.HasValue && subjectNames.TryGetValue(x.SubjectId.Value, out var subjectName) ? subjectName : null,
            mayViewStudents ? studentCounts.GetValueOrDefault(x.ClassSectionId) : null)).ToList();
    }

    private async Task<PortalDashboard> AdministrativeDashboardAsync(string audience, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        var metrics = new List<PortalDashboardMetric>();
        if (permissions.Contains(Permissions.SchoolsView))
            metrics.Add(new("campuses", "Active campuses", await db.Campuses.LongCountAsync(x => x.IsActive, ct), "/portal/school"));
        if (await CanUseAsync(permissions, Permissions.AdmissionsView, FeatureKeys.Admissions, ct))
            metrics.Add(new("applicants", "Applications", await db.Applicants.LongCountAsync(ct), "/portal/admissions"));
        if (await CanUseAsync(permissions, Permissions.StudentsView, FeatureKeys.StudentInformation, ct))
            metrics.Add(new("students", "Active students", await db.Students.LongCountAsync(x => x.Status == StudentStatus.Active, ct), "/portal/students"));
        if (await CanUseAsync(permissions, Permissions.StaffView, FeatureKeys.StaffManagement, ct))
            metrics.Add(new("staff", "Staff records", await db.StaffProfiles.LongCountAsync(ct), "/portal/staff"));
        return new(audience, metrics, audience == "SuperAdmin"
            ? "This Phase 1 view is limited to the selected tenant. Platform-wide administration requires a separate platform security boundary."
            : "Review the school core records that your permissions and subscription make available.");
    }

    private async Task<PortalDashboard> StaffDashboardAsync(Guid userId, string audience, bool teacherView, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        var staffId = await db.StaffProfiles.Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var assignments = staffId.HasValue ? db.TeachingAssignments.Where(x => x.StaffId == staffId.Value) : db.TeachingAssignments.Where(_ => false);
        var metrics = new List<PortalDashboardMetric>();
        if (await CanUseAsync(permissions, Permissions.StaffView, FeatureKeys.StaffManagement, ct))
            metrics.Add(new("profile", "Linked staff profile", staffId.HasValue ? 1 : 0, staffId.HasValue ? $"/portal/staff/{staffId}" : null));
        if (teacherView)
        {
            if (await CanUseAsync(permissions, Permissions.AcademicsView, FeatureKeys.AcademicStructure, ct))
                metrics.Add(new("classes", "Assigned classes", await assignments.Select(x => x.ClassSectionId).Distinct().LongCountAsync(ct), "/portal/academics"));
            if (await CanUseAsync(permissions, Permissions.StudentsView, FeatureKeys.StudentInformation, ct))
                metrics.Add(new("students", "Students in assigned classes", await ScopedStudents(userId).LongCountAsync(ct), "/portal/students"));
        }
        return new(audience, metrics, teacherView
            ? "Student totals include only learners in your assigned classes."
            : "Your staff workspace exposes only your linked profile until additional staff workflows are delivered.");
    }

    private async Task<PortalDashboard> ParentDashboardAsync(Guid userId, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        var metrics = new List<PortalDashboardMetric>();
        if (await CanUseAsync(permissions, Permissions.StudentsView, FeatureKeys.StudentInformation, ct))
            metrics.Add(new("children", "Linked children", await ScopedStudents(userId).LongCountAsync(ct), "/portal/students"));
        return new("Parent", metrics,
            "Only students connected to your guardian account are included.");
    }

    private async Task<PortalDashboard> StudentDashboardAsync(Guid userId, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        if (!await CanUseAsync(permissions, Permissions.StudentsView, FeatureKeys.StudentInformation, ct))
            return new("Student", [], "Your student-information subscription or permission is not currently active.");
        var studentIds = db.Students.Where(x => x.UserId == userId).Select(x => x.Id);
        var profileCount = await studentIds.LongCountAsync(ct);
        var activeEnrollmentCount = await db.Enrollments.LongCountAsync(x => studentIds.Contains(x.StudentId) && x.Status == EnrollmentStatus.Active, ct);
        return new("Student",
            [new("profile", "Linked student profile", profileCount, "/portal/students"), new("enrollments", "Current enrolments", activeEnrollmentCount, "/portal/students")],
            "Only your own student record and current enrolment are included.");
    }

    private async Task<PortalDashboard> AccountantDashboardAsync(IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        var metrics = new List<PortalDashboardMetric>();
        if (permissions.Contains(Permissions.SchoolsView))
            metrics.Add(new("campuses", "Active campuses", await db.Campuses.LongCountAsync(x => x.IsActive, ct), "/portal/school"));
        return new("Accountant", metrics, "The accountant workspace is established in Phase 1; finance operations are introduced with the Phase 4 finance modules.");
    }

    private IQueryable<Student> ScopedStudents(Guid userId) => db.Students.Where(student =>
        student.UserId == userId ||
        db.StudentGuardians.Any(link => link.StudentId == student.Id && db.Guardians.Any(guardian => guardian.Id == link.GuardianId && guardian.UserId == userId)) ||
        db.Enrollments.Any(enrollment => enrollment.StudentId == student.Id && enrollment.Status == EnrollmentStatus.Active &&
            db.TeachingAssignments.Any(assignment => assignment.ClassSectionId == enrollment.ClassSectionId && db.StaffProfiles.Any(staff => staff.Id == assignment.StaffId && staff.UserId == userId))));

    private async Task<bool> CanUseAsync(IReadOnlyCollection<string> permissions, string permission, string feature, CancellationToken ct) =>
        permissions.Contains(permission) && (await entitlements.GetAsync(feature, null, ct)).Enabled;
}
