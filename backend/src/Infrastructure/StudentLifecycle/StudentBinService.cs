using GiddyEdu.BuildingBlocks.Api;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

public sealed record StudentBinRow(Guid Id, string AdmissionNumber, string FirstName, string? MiddleName,
    string LastName, DateTimeOffset DeletedAtUtc, string? DeletedByName);

public interface IStudentBinService
{
    Task<long> CountAsync(Guid actor, CancellationToken ct = default);
    Task<PageResult<StudentBinRow>> ListAsync(Guid actor, int page, int pageSize, string? search, CancellationToken ct = default);
    Task ChangeStatusAsync(Guid actor, IReadOnlyCollection<Guid> studentIds, StudentStatus status, CancellationToken ct = default);
    Task MoveAsync(Guid actor, IReadOnlyCollection<Guid> studentIds, CancellationToken ct = default);
    Task RestoreAsync(Guid actor, Guid studentId, CancellationToken ct = default);
    Task PermanentlyDeleteAsync(Guid actor, Guid studentId, CancellationToken ct = default);
}

public sealed class StudentBinService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access,
    IPermissionService permissions, IFileObjectStorage storage, IClock clock) : IStudentBinService
{
    private IQueryable<Student> Bin => db.Students.IgnoreQueryFilters(["BinFilter"]).Where(x => x.DeletedAtUtc != null);
    private Task DemandAsync(Guid actor, CancellationToken ct) =>
        access.DemandAsync(actor, Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);

    public async Task<long> CountAsync(Guid actor, CancellationToken ct = default)
    { await DemandAsync(actor, ct); return await Bin.LongCountAsync(ct); }

    public async Task<PageResult<StudentBinRow>> ListAsync(Guid actor, int page, int pageSize, string? search, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct); page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Bin.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.FirstName.ToLower().Contains(term) || x.LastName.ToLower().Contains(term)
                || x.AdmissionNumber.ToLower().Contains(term));
        }
        var total = await query.LongCountAsync(ct);
        var rows = await (from student in query
                          join user in db.Users.AsNoTracking() on student.DeletedByUserId equals user.Id into actors
                          from actorUser in actors.DefaultIfEmpty()
                          orderby student.DeletedAtUtc descending, student.Id
                          select new StudentBinRow(student.Id, student.AdmissionNumber, student.FirstName, student.MiddleName,
                              student.LastName, student.DeletedAtUtc!.Value, actorUser != null ? actorUser.DisplayName : null))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows, page, pageSize, total);
    }

    public async Task ChangeStatusAsync(Guid actor, IReadOnlyCollection<Guid> studentIds, StudentStatus status, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct); var ids = ValidateIds(studentIds);
        var students = await db.Students.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (students.Count != ids.Length) throw new KeyNotFoundException("One or more students were not found.");
        foreach (var student in students) { student.ChangeStatus(status); Audit(actor, "Student.StatusChange", student.Id); }
        await db.SaveChangesAsync(ct);
    }

    public async Task MoveAsync(Guid actor, IReadOnlyCollection<Guid> studentIds, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct); var ids = ValidateIds(studentIds);
        var students = await db.Students.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (students.Count != ids.Length) throw new KeyNotFoundException("One or more students were not found.");
        foreach (var student in students)
        {
            if (student.UserId == actor) throw new InvalidOperationException("You cannot move your own student record to the bin.");
            Guid? removedRole = null; var membershipSuspended = false;
            if (student.UserId is Guid userId)
            {
                if (!await permissions.HasPermissionAsync(actor, Permissions.RolesManage, ct))
                    throw new UnauthorizedAccessException("Roles.Manage permission is required to suspend a linked student account.");
                var membership = await db.TenantMemberships.SingleOrDefaultAsync(x => x.UserId == userId, ct);
                if (membership is not null)
                {
                    var assignment = await (from link in db.TenantMembershipRoles join role in db.TenantRoles on link.RoleId equals role.Id
                                            where link.MembershipId == membership.Id && role.Name == SystemRoleTemplates.Student.Name select link)
                        .SingleOrDefaultAsync(ct);
                    if (assignment is not null) { removedRole = assignment.RoleId; db.TenantMembershipRoles.Remove(assignment); }
                    var otherRoles = await db.TenantMembershipRoles.AnyAsync(x => x.MembershipId == membership.Id && x.RoleId != removedRole, ct);
                    var otherProfile = await db.StaffProfiles.AnyAsync(x => x.UserId == userId, ct) || await db.Guardians.AnyAsync(x => x.UserId == userId, ct);
                    if (membership.IsActive && !otherRoles && !otherProfile) { membership.Suspend(); membershipSuspended = true; }
                }
                foreach (var token in await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAtUtc == null).ToListAsync(ct)) token.Revoke(clock.UtcNow);
            }
            foreach (var invitation in await db.AccountInvitations.Where(x => x.TargetType == InvitationTargetType.Student && x.TargetId == student.Id && x.AcceptedAtUtc == null && x.RevokedAtUtc == null).ToListAsync(ct)) invitation.Revoke(clock.UtcNow);
            student.MoveToBin(actor, clock.UtcNow, removedRole, membershipSuspended); Audit(actor, "Student.MoveToBin", student.Id);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task RestoreAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct); var student = await Bin.SingleOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new KeyNotFoundException("Student was not found in the bin.");
        if (student.UserId is Guid userId && (student.SuspendedStudentRoleId.HasValue || student.MembershipSuspendedForBin))
        {
            if (!await permissions.HasPermissionAsync(actor, Permissions.RolesManage, ct)) throw new UnauthorizedAccessException("Roles.Manage permission is required to restore a linked student account.");
            var membership = await db.TenantMemberships.SingleOrDefaultAsync(x => x.UserId == userId, ct) ?? throw new InvalidOperationException("The linked account no longer has a school membership.");
            if (student.SuspendedStudentRoleId is Guid roleId && await db.TenantRoles.AnyAsync(x => x.Id == roleId, ct) && !await db.TenantMembershipRoles.AnyAsync(x => x.MembershipId == membership.Id && x.RoleId == roleId, ct))
                db.TenantMembershipRoles.Add(new TenantMembershipRole(RequireTenant(), membership.Id, roleId));
            if (student.MembershipSuspendedForBin) membership.Reactivate();
        }
        student.Restore(); Audit(actor, "Student.Restore", studentId); await db.SaveChangesAsync(ct);
    }

    public async Task PermanentlyDeleteAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct); var student = await Bin.SingleOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new KeyNotFoundException("Student was not found in the bin.");
        var files = await db.StoredFiles.Where(x => x.EntityType == "Student" && x.EntityId == studentId).ToListAsync(ct);
        foreach (var file in files) await storage.DeleteAsync(file.ObjectKey, ct);
        db.StoredFiles.RemoveRange(files);
        db.AccountInvitations.RemoveRange(await db.AccountInvitations.Where(x => x.TargetType == InvitationTargetType.Student && x.TargetId == studentId).ToListAsync(ct));
        db.StudentGuardians.RemoveRange(await db.StudentGuardians.IgnoreQueryFilters().Where(x => x.TenantId == RequireTenant() && x.StudentId == studentId).ToListAsync(ct));
        db.Students.Remove(student); Audit(actor, "Student.PermanentDelete", studentId); await db.SaveChangesAsync(ct);
    }

    private static Guid[] ValidateIds(IReadOnlyCollection<Guid> ids)
    { var values = ids.Where(x => x != Guid.Empty).Distinct().Take(101).ToArray(); if (values.Length == 0 || values.Length > 100) throw new ArgumentException("Select between 1 and 100 students."); return values; }
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    private void Audit(Guid actor, string action, Guid id) => db.AuditRecords.Add(new AuditRecord(Guid.NewGuid(), RequireTenant(), actor, action, "Student", id.ToString(), "Succeeded", clock.UtcNow, null));
}
