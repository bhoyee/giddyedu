using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Authorization;

public sealed record AccessPresentation(IReadOnlyList<string> Roles, IReadOnlyList<string> Audiences, string DefaultAudience);

public interface IAccessProfileService
{
    Task<AccessPresentation> GetPresentationAsync(Guid userId, IReadOnlyCollection<string> effectivePermissions, CancellationToken ct = default);
}

public sealed class AccessProfileService(GiddyEduDbContext db) : IAccessProfileService
{
    public async Task<AccessPresentation> GetPresentationAsync(Guid userId, IReadOnlyCollection<string> effectivePermissions, CancellationToken ct = default)
    {
        var roles = await (from membership in db.TenantMemberships
                           join assignment in db.TenantMembershipRoles on membership.Id equals assignment.MembershipId
                           join role in db.TenantRoles on assignment.RoleId equals role.Id
                           where membership.UserId == userId && membership.IsActive
                           select role.Name).Distinct().OrderBy(x => x).ToListAsync(ct);
        var staffId = await db.StaffProfiles.Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var isTeacher = staffId.HasValue && await db.TeachingAssignments.AnyAsync(x => x.StaffId == staffId.Value, ct);
        var isGuardian = await db.Guardians.AnyAsync(x => x.UserId == userId, ct);
        return PortalAudienceResolver.Resolve(roles, effectivePermissions, staffId.HasValue, isTeacher, isGuardian);
    }
}

public static class PortalAudienceResolver
{
    public static AccessPresentation Resolve(IReadOnlyList<string> roles, IReadOnlyCollection<string> permissions, bool isStaff, bool isTeacher, bool isGuardian)
    {
        var names = roles.Select(x => x.Trim().ToLowerInvariant()).ToArray();
        var audiences = new List<string>();
        if (names.Any(x => x.Contains("super admin") || x.Contains("platform administrator"))) audiences.Add("SuperAdmin");
        if (permissions.Contains(Permissions.TenantSettingsManage) || permissions.Contains(Permissions.RolesManage)) audiences.Add("SchoolAdmin");
        if (isTeacher || names.Any(x => x.Contains("teacher"))) audiences.Add("Teacher");
        if (isStaff || names.Any(x => x is "staff" or "employee")) audiences.Add("Staff");
        if (isGuardian || names.Any(x => x.Contains("parent") || x.Contains("guardian"))) audiences.Add("Parent");
        if (names.Any(x => x is "student" or "learner")) audiences.Add("Student");
        if (names.Any(x => x.Contains("accountant") || x.Contains("bursar") || x.Contains("finance"))) audiences.Add("Accountant");
        if (audiences.Count == 0) audiences.Add("Staff");
        var preferred = new[] { "SuperAdmin", "SchoolAdmin", "Teacher", "Staff", "Parent", "Student", "Accountant" }.First(audiences.Contains);
        return new(roles, audiences, preferred);
    }
}
