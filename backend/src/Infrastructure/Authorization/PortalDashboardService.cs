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
public sealed record FamilyStudentSummary(Guid StudentId, string AdmissionNumber, string FirstName, string LastName, string Relationship, bool IsPrimaryGuardian, Guid? ClassSectionId, string? ClassName);
public sealed record StudentEnrollmentSummary(Guid EnrollmentId, Guid ClassSectionId, string ClassName, string ClassCode, Guid AcademicYearId, string AcademicYearName, DateOnly EnrolledOn);
public sealed record StudentSelfService(Guid StudentId, string AdmissionNumber, string FirstName, string LastName, DateOnly DateOfBirth, string? Email, string Status, StudentEnrollmentSummary? CurrentEnrollment);
public sealed record StaffSelfService(Guid StaffId, string StaffNumber, string FirstName, string LastName, string Category, string Status, string CampusName, string? DepartmentName, string? PositionName, string? WorkEmail, string? Phone, DateOnly HireDate, long TeachingAssignmentCount);
public sealed record OperationalReadinessStep(string Key, string Label, bool Complete, string Detail, string Href);
public sealed record OperationalReadiness(int CompletedSteps, int TotalSteps, int Percentage, IReadOnlyList<OperationalReadinessStep> Steps);
public sealed record CommercialFeatureSummary(string Key, bool Enabled, long? Limit);
public sealed record CommercialOverview(string? PlanCode, string? PlanName, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc, long ActiveCampusCount, IReadOnlyList<CommercialFeatureSummary> Features);

public interface IPortalDashboardService
{
    Task<PortalDashboard> GetAsync(Guid userId, string audience, CancellationToken ct = default);
    Task<IReadOnlyList<TeachingClassSummary>> GetTeachingClassesAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<FamilyStudentSummary>> GetFamilyStudentsAsync(Guid userId, CancellationToken ct = default);
    Task<StudentSelfService?> GetStudentSelfServiceAsync(Guid userId, CancellationToken ct = default);
    Task<StaffSelfService?> GetStaffSelfServiceAsync(Guid userId, CancellationToken ct = default);
    Task<OperationalReadiness> GetOperationalReadinessAsync(Guid userId, CancellationToken ct = default);
    Task<CommercialOverview> GetCommercialOverviewAsync(Guid userId, CancellationToken ct = default);
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
        var isPlatformAdministrator = await (from assignment in db.UserRoles join role in db.Roles on assignment.RoleId equals role.Id where assignment.UserId == userId && role.Name == GlobalRoles.PlatformAdministrator select role.Id).AnyAsync(ct);
        var canonicalAudience = string.Equals(audience, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && isPlatformAdministrator
            ? "SuperAdmin"
            : presentation.Audiences.SingleOrDefault(x => string.Equals(x, audience, StringComparison.OrdinalIgnoreCase));
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

    public async Task<IReadOnlyList<FamilyStudentSummary>> GetFamilyStudentsAsync(Guid userId, CancellationToken ct = default)
    {
        var effectivePermissions = await permissions.GetEffectivePermissionsAsync(userId, ct);
        var presentation = await profiles.GetPresentationAsync(userId, effectivePermissions, ct);
        if (!presentation.Audiences.Contains("Parent") || !await CanUseAsync(effectivePermissions, Permissions.StudentsView, FeatureKeys.StudentInformation, ct))
            throw new UnauthorizedAccessException("The family workspace is not available for this account.");

        var rows = await (from guardian in db.Guardians.AsNoTracking()
                          join link in db.StudentGuardians.AsNoTracking() on guardian.Id equals link.GuardianId
                          join student in db.Students.AsNoTracking() on link.StudentId equals student.Id
                          where guardian.UserId == userId
                          orderby student.LastName, student.FirstName
                          select new { student.Id, student.AdmissionNumber, student.FirstName, student.LastName, link.Relationship, link.IsPrimary }).ToListAsync(ct);
        var studentIds = rows.Select(x => x.Id).Distinct().ToArray();
        var enrollments = await (from enrollment in db.Enrollments.AsNoTracking()
                                 join section in db.ClassSections.AsNoTracking() on enrollment.ClassSectionId equals section.Id
                                 where studentIds.Contains(enrollment.StudentId) && enrollment.Status == EnrollmentStatus.Active
                                 select new { enrollment.StudentId, section.Id, section.Name }).ToDictionaryAsync(x => x.StudentId, ct);
        return rows.Select(x =>
        {
            var hasEnrollment = enrollments.TryGetValue(x.Id, out var enrollment);
            return new FamilyStudentSummary(x.Id, x.AdmissionNumber, x.FirstName, x.LastName, x.Relationship.ToString(), x.IsPrimary,
                hasEnrollment ? enrollment!.Id : null, hasEnrollment ? enrollment!.Name : null);
        }).ToList();
    }

    public async Task<StudentSelfService?> GetStudentSelfServiceAsync(Guid userId, CancellationToken ct = default)
    {
        var effectivePermissions = await permissions.GetEffectivePermissionsAsync(userId, ct);
        var presentation = await profiles.GetPresentationAsync(userId, effectivePermissions, ct);
        if (!presentation.Audiences.Contains("Student") || !await CanUseAsync(effectivePermissions, Permissions.StudentsView, FeatureKeys.StudentInformation, ct))
            throw new UnauthorizedAccessException("The student workspace is not available for this account.");

        var student = await db.Students.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.Id, x.AdmissionNumber, x.FirstName, x.LastName, x.DateOfBirth, x.Email, x.Status }).SingleOrDefaultAsync(ct);
        if (student is null) return null;
        var enrollment = await (from current in db.Enrollments.AsNoTracking()
                                join section in db.ClassSections.AsNoTracking() on current.ClassSectionId equals section.Id
                                join year in db.AcademicYears.AsNoTracking() on current.AcademicYearId equals year.Id
                                where current.StudentId == student.Id && current.Status == EnrollmentStatus.Active
                                orderby current.EnrolledOn descending
                                select new StudentEnrollmentSummary(current.Id, section.Id, section.Name, section.Code, year.Id, year.Name, current.EnrolledOn)).FirstOrDefaultAsync(ct);
        return new(student.Id, student.AdmissionNumber, student.FirstName, student.LastName, student.DateOfBirth, student.Email, student.Status.ToString(), enrollment);
    }

    public async Task<StaffSelfService?> GetStaffSelfServiceAsync(Guid userId, CancellationToken ct = default)
    {
        var effectivePermissions = await permissions.GetEffectivePermissionsAsync(userId, ct);
        var presentation = await profiles.GetPresentationAsync(userId, effectivePermissions, ct);
        if ((!presentation.Audiences.Contains("Staff") && !presentation.Audiences.Contains("Teacher")) || !await CanUseAsync(effectivePermissions, Permissions.StaffView, FeatureKeys.StaffManagement, ct))
            throw new UnauthorizedAccessException("The staff workspace is not available for this account.");

        var staff = await db.StaffProfiles.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.Id, x.StaffNumber, x.FirstName, x.LastName, x.Category, x.Status, x.CampusId, x.DepartmentId, x.PositionId, x.WorkEmail, x.Phone, x.HireDate }).SingleOrDefaultAsync(ct);
        if (staff is null) return null;
        var campusName = await db.Campuses.Where(x => x.Id == staff.CampusId).Select(x => x.Name).SingleAsync(ct);
        var departmentName = staff.DepartmentId.HasValue ? await db.Departments.Where(x => x.Id == staff.DepartmentId.Value).Select(x => x.Name).SingleOrDefaultAsync(ct) : null;
        var positionName = staff.PositionId.HasValue ? await db.Positions.Where(x => x.Id == staff.PositionId.Value).Select(x => x.Name).SingleOrDefaultAsync(ct) : null;
        var assignmentCount = await db.TeachingAssignments.LongCountAsync(x => x.StaffId == staff.Id, ct);
        return new(staff.Id, staff.StaffNumber, staff.FirstName, staff.LastName, staff.Category.ToString(), staff.Status.ToString(), campusName,
            departmentName, positionName, staff.WorkEmail, staff.Phone, staff.HireDate, assignmentCount);
    }

    public async Task<OperationalReadiness> GetOperationalReadinessAsync(Guid userId, CancellationToken ct = default)
    {
        var effectivePermissions = await permissions.GetEffectivePermissionsAsync(userId, ct);
        var presentation = await profiles.GetPresentationAsync(userId, effectivePermissions, ct);
        if (!presentation.Audiences.Contains("SchoolAdmin")) throw new UnauthorizedAccessException("The school management workspace is not available for this account.");

        var steps = new List<OperationalReadinessStep>();
        if (await CanUseAsync(effectivePermissions, Permissions.SchoolsView, FeatureKeys.SchoolAdministration, ct))
        {
            steps.Add(new("school-profile", "Complete school profile", await db.SchoolProfiles.AnyAsync(ct), "Add the school's identity and contact information.", "/portal/school"));
            steps.Add(new("campus", "Configure an active campus", await db.Campuses.AnyAsync(x => x.IsActive, ct), "At least one active campus is required for operations.", "/portal/school"));
        }
        if (await CanUseAsync(effectivePermissions, Permissions.AcademicsView, FeatureKeys.AcademicStructure, ct))
            steps.Add(new("academics", "Configure academic structure", await db.AcademicYears.AnyAsync(ct) && await db.ClassSections.AnyAsync(x => x.IsActive, ct), "Create an academic year and at least one active class.", "/portal/academics"));
        if (await CanUseAsync(effectivePermissions, Permissions.StaffView, FeatureKeys.StaffManagement, ct))
            steps.Add(new("staff", "Create staff profiles", await db.StaffProfiles.AnyAsync(ct), "Add the people who operate and teach in the school.", "/portal/staff"));
        if (await CanUseAsync(effectivePermissions, Permissions.StudentsView, FeatureKeys.StudentInformation, ct))
            steps.Add(new("students", "Enrol students", await db.Enrollments.AnyAsync(x => x.Status == EnrollmentStatus.Active, ct), "Create learner records and place them into classes.", "/portal/students"));
        if (await CanUseAsync(effectivePermissions, Permissions.GuardiansView, FeatureKeys.GuardianManagement, ct))
            steps.Add(new("guardians", "Connect families", await db.StudentGuardians.AnyAsync(ct), "Link at least one guardian relationship to a learner.", "/portal/guardians"));
        if (effectivePermissions.Contains(Permissions.TenantSettingsManage))
            steps.Add(new("subscription", "Activate a subscription", await db.TenantSubscriptions.AnyAsync(x => x.IsActive, ct), "Confirm an active plan for the school.", "/portal/subscription"));
        var completed = steps.Count(x => x.Complete);
        return new(completed, steps.Count, steps.Count == 0 ? 0 : (int)Math.Round(completed * 100m / steps.Count, MidpointRounding.AwayFromZero), steps);
    }

    public async Task<CommercialOverview> GetCommercialOverviewAsync(Guid userId, CancellationToken ct = default)
    {
        var effectivePermissions = await permissions.GetEffectivePermissionsAsync(userId, ct);
        var presentation = await profiles.GetPresentationAsync(userId, effectivePermissions, ct);
        if (!presentation.Audiences.Contains("Accountant") || !effectivePermissions.Contains(Permissions.SchoolsView))
            throw new UnauthorizedAccessException("The accountant workspace is not available for this account.");

        var subscription = await (from current in db.TenantSubscriptions.AsNoTracking()
                                  join plan in db.Plans.AsNoTracking() on current.PlanId equals plan.Id
                                  where current.IsActive
                                  orderby current.StartsAtUtc descending
                                  select new { plan.Code, plan.Name, current.StartsAtUtc, current.EndsAtUtc }).FirstOrDefaultAsync(ct);
        var features = new List<CommercialFeatureSummary>();
        foreach (var key in FeatureKeys.PhaseOne)
        {
            var entitlement = await entitlements.GetAsync(key, null, ct);
            features.Add(new(key, entitlement.Enabled, entitlement.Limit));
        }
        return new(subscription?.Code, subscription?.Name, subscription?.StartsAtUtc, subscription?.EndsAtUtc,
            await db.Campuses.LongCountAsync(x => x.IsActive, ct), features);
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
            metrics.Add(new("profile", "Linked staff profile", staffId.HasValue ? 1 : 0, staffId.HasValue ? "/portal/staff-self-service" : null));
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
            metrics.Add(new("children", "Linked children", await ScopedStudents(userId).LongCountAsync(ct), "/portal/family"));
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
            [new("profile", "Linked student profile", profileCount, "/portal/student"), new("enrollments", "Current enrolments", activeEnrollmentCount, "/portal/student")],
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
