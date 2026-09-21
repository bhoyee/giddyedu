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
using System.Text.Json;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

public sealed record GuardianBinRow(Guid Id, string FirstName, string LastName, string Phone, string? Email,
    DateTimeOffset DeletedAtUtc, string? DeletedByName);
public sealed record GuardianStudentLinkRow(Guid StudentId, string AdmissionNumber, string FirstName, string? MiddleName,
    string LastName, string? ClassSectionName, string Relationship);

public sealed class GuardianStudentLinksExistException(int studentCount, string operation)
    : InvalidOperationException($"This guardian is linked to {studentCount} student{(studentCount == 1 ? "" : "s")}. Unlink the student records before {operation}.")
{
    public int StudentCount { get; } = studentCount;
}

public interface IGuardianBinService
{
    Task<long> CountAsync(Guid actor, CancellationToken ct = default);
    Task<PageResult<GuardianBinRow>> ListAsync(Guid actor, int page, int pageSize, string? search = null, CancellationToken ct = default);
    Task<IReadOnlyCollection<GuardianStudentLinkRow>> ListStudentLinksAsync(Guid actor, Guid guardianId, CancellationToken ct = default);
    Task UnlinkStudentAsync(Guid actor, Guid guardianId, Guid studentId, CancellationToken ct = default);
    Task UnlinkAllStudentsAsync(Guid actor, Guid guardianId, CancellationToken ct = default);
    Task MoveAsync(Guid actor, Guid guardianId, bool unlinkStudents = false, CancellationToken ct = default);
    Task RestoreAsync(Guid actor, Guid guardianId, CancellationToken ct = default);
    Task PermanentlyDeleteAsync(Guid actor, Guid guardianId, bool unlinkStudents = false, CancellationToken ct = default);
}

public sealed class GuardianBinService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access,
    IPermissionService permissions, IFileObjectStorage storage, IClock clock) : IGuardianBinService
{
    private IQueryable<Guardian> Bin => db.Guardians.IgnoreQueryFilters(["BinFilter"]).Where(x => x.DeletedAtUtc != null);
    private Task DemandAsync(Guid actor, CancellationToken ct) =>
        access.DemandAsync(actor, Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);

    public async Task<long> CountAsync(Guid actor, CancellationToken ct = default)
    { await DemandAsync(actor, ct); return await Bin.LongCountAsync(ct); }

    public async Task<PageResult<GuardianBinRow>> ListAsync(Guid actor, int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Bin.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.FirstName.ToLower().Contains(term) || x.LastName.ToLower().Contains(term)
                || x.Phone.Contains(term) || x.Email != null && x.Email.ToLower().Contains(term));
        }
        var total = await query.LongCountAsync(ct);
        var rows = await (from guardian in query
                          join user in db.Users.AsNoTracking() on guardian.DeletedByUserId equals user.Id into actors
                          from actorUser in actors.DefaultIfEmpty()
                          orderby guardian.DeletedAtUtc descending, guardian.Id
                          select new GuardianBinRow(guardian.Id, guardian.FirstName, guardian.LastName, guardian.Phone,
                              guardian.Email, guardian.DeletedAtUtc!.Value, actorUser != null ? actorUser.DisplayName : null))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows, page, pageSize, total);
    }

    public async Task<IReadOnlyCollection<GuardianStudentLinkRow>> ListStudentLinksAsync(Guid actor, Guid guardianId, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        if (!await db.Guardians.AnyAsync(x => x.Id == guardianId, ct)) throw new KeyNotFoundException("Guardian was not found.");
        return await (from link in db.StudentGuardians.AsNoTracking()
                      join student in db.Students.AsNoTracking() on link.StudentId equals student.Id
                      where link.GuardianId == guardianId
                      let className = (from enrollment in db.Enrollments.AsNoTracking()
                                       join section in db.ClassSections.AsNoTracking() on enrollment.ClassSectionId equals section.Id
                                       where enrollment.StudentId == student.Id && enrollment.Status == EnrollmentStatus.Active
                                       orderby enrollment.CreatedAtUtc descending
                                       select section.Name).FirstOrDefault()
                      orderby student.LastName, student.FirstName, student.Id
                      select new GuardianStudentLinkRow(student.Id, student.AdmissionNumber, student.FirstName,
                          student.MiddleName, student.LastName, className, link.Relationship.ToString()))
            .ToListAsync(ct);
    }

    public async Task UnlinkStudentAsync(Guid actor, Guid guardianId, Guid studentId, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        var link = await db.StudentGuardians.SingleOrDefaultAsync(x => x.GuardianId == guardianId && x.StudentId == studentId, ct)
            ?? throw new KeyNotFoundException("The guardian is no longer linked to this student.");
        db.StudentGuardians.Remove(link);
        Audit(actor, "Guardian.UnlinkStudent", guardianId, JsonSerializer.Serialize(new { studentId }));
        await db.SaveChangesAsync(ct);
    }

    public async Task UnlinkAllStudentsAsync(Guid actor, Guid guardianId, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        if (!await db.Guardians.AnyAsync(x => x.Id == guardianId, ct)) throw new KeyNotFoundException("Guardian was not found.");
        var links = await db.StudentGuardians.Where(x => x.GuardianId == guardianId).ToListAsync(ct);
        if (links.Count == 0) return;
        db.StudentGuardians.RemoveRange(links);
        Audit(actor, "Guardian.UnlinkAllStudents", guardianId, JsonSerializer.Serialize(new { studentCount = links.Count }));
        await db.SaveChangesAsync(ct);
    }

    public async Task MoveAsync(Guid actor, Guid guardianId, bool unlinkStudents = false, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        var guardian = await db.Guardians.SingleOrDefaultAsync(x => x.Id == guardianId, ct)
            ?? throw new KeyNotFoundException("Guardian was not found.");
        if (guardian.UserId == actor) throw new InvalidOperationException("You cannot move your own guardian record to the bin.");
        if (guardian.UserId.HasValue && !await permissions.HasPermissionAsync(actor, Permissions.RolesManage, ct))
            throw new UnauthorizedAccessException("Roles.Manage permission is required to suspend a linked guardian account.");
        var studentLinks = await db.StudentGuardians.Where(x => x.GuardianId == guardianId).ToListAsync(ct);
        if (studentLinks.Count > 0 && !unlinkStudents)
            throw new GuardianStudentLinksExistException(studentLinks.Count, "moving the guardian to the bin");
        if (studentLinks.Count > 0)
        {
            db.StudentGuardians.RemoveRange(studentLinks);
            Audit(actor, "Guardian.UnlinkStudentsForBin", guardianId);
        }

        Guid? removedParentRole = null;
        var membershipSuspended = false;
        if (guardian.UserId is Guid userId && !await db.Guardians.AnyAsync(x => x.Id != guardianId && x.UserId == userId, ct))
        {
            var membership = await db.TenantMemberships.SingleOrDefaultAsync(x => x.UserId == userId, ct);
            if (membership is not null)
            {
                var parentAssignment = await (from assignment in db.TenantMembershipRoles
                                              join role in db.TenantRoles on assignment.RoleId equals role.Id
                                              where assignment.MembershipId == membership.Id && role.Name == SystemRoleTemplates.Parent.Name
                                              select assignment).SingleOrDefaultAsync(ct);
                if (parentAssignment is not null)
                { removedParentRole = parentAssignment.RoleId; db.TenantMembershipRoles.Remove(parentAssignment); }
                var otherRoles = await db.TenantMembershipRoles.AnyAsync(x => x.MembershipId == membership.Id && x.RoleId != removedParentRole, ct);
                var otherProfile = await db.StaffProfiles.AnyAsync(x => x.UserId == userId, ct)
                    || await db.Students.AnyAsync(x => x.UserId == userId, ct);
                if (membership.IsActive && !otherRoles && !otherProfile)
                { membership.Suspend(); membershipSuspended = true; }
            }
        }
        foreach (var invitation in await db.AccountInvitations.Where(x => x.TargetType == InvitationTargetType.Guardian
                     && x.TargetId == guardianId && x.AcceptedAtUtc == null && x.RevokedAtUtc == null).ToListAsync(ct))
            invitation.Revoke(clock.UtcNow);
        if (guardian.UserId is Guid linkedUser)
            foreach (var token in await db.RefreshTokens.Where(x => x.UserId == linkedUser && x.RevokedAtUtc == null).ToListAsync(ct))
                token.Revoke(clock.UtcNow);
        guardian.MoveToBin(actor, clock.UtcNow, removedParentRole, membershipSuspended);
        Audit(actor, "Guardian.MoveToBin", guardianId);
        await db.SaveChangesAsync(ct);
    }

    public async Task RestoreAsync(Guid actor, Guid guardianId, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        var guardian = await Bin.SingleOrDefaultAsync(x => x.Id == guardianId, ct)
            ?? throw new KeyNotFoundException("Guardian was not found in the bin.");
        if (guardian.UserId is Guid userId && (guardian.SuspendedParentRoleId.HasValue || guardian.MembershipSuspendedForBin))
        {
            if (!await permissions.HasPermissionAsync(actor, Permissions.RolesManage, ct))
                throw new UnauthorizedAccessException("Roles.Manage permission is required to restore a linked guardian account.");
            var membership = await db.TenantMemberships.SingleOrDefaultAsync(x => x.UserId == userId, ct)
                ?? throw new InvalidOperationException("The linked account no longer has a school membership.");
            if (guardian.SuspendedParentRoleId is Guid roleId
                && await db.TenantRoles.AnyAsync(x => x.Id == roleId, ct)
                && !await db.TenantMembershipRoles.AnyAsync(x => x.MembershipId == membership.Id && x.RoleId == roleId, ct))
                db.TenantMembershipRoles.Add(new TenantMembershipRole(RequireTenant(), membership.Id, roleId));
            if (guardian.MembershipSuspendedForBin) membership.Reactivate();
        }
        guardian.Restore();
        Audit(actor, "Guardian.Restore", guardianId);
        await db.SaveChangesAsync(ct);
    }

    public async Task PermanentlyDeleteAsync(Guid actor, Guid guardianId, bool unlinkStudents = false, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        var guardian = await Bin.SingleOrDefaultAsync(x => x.Id == guardianId, ct)
            ?? throw new KeyNotFoundException("Guardian was not found in the bin.");
        if (guardian.UserId == actor) throw new InvalidOperationException("You cannot permanently delete your own guardian record.");
        var studentLinks = await db.StudentGuardians.IgnoreQueryFilters().Where(x => x.TenantId == RequireTenant() && x.GuardianId == guardianId).ToListAsync(ct);
        if (studentLinks.Count > 0 && !unlinkStudents)
            throw new GuardianStudentLinksExistException(studentLinks.Count, "permanently deleting the guardian");
        var files = await db.StoredFiles.Where(x => x.EntityType == "Guardian" && x.EntityId == guardianId).ToListAsync(ct);
        foreach (var file in files) await storage.DeleteAsync(file.ObjectKey, ct);
        db.StoredFiles.RemoveRange(files);
        db.AccountInvitations.RemoveRange(await db.AccountInvitations.Where(x => x.TargetType == InvitationTargetType.Guardian
            && x.TargetId == guardianId).ToListAsync(ct));
        db.StudentGuardians.RemoveRange(studentLinks);
        db.Guardians.Remove(guardian);
        Audit(actor, "Guardian.PermanentDelete", guardianId);
        await db.SaveChangesAsync(ct);
    }

    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    private void Audit(Guid actor, string action, Guid guardianId, string? details = null) =>
        db.AuditRecords.Add(new AuditRecord(Guid.NewGuid(), RequireTenant(), actor, action, "Guardian", guardianId.ToString(),
            "Succeeded", clock.UtcNow, details));
}
