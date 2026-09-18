using GiddyEdu.BuildingBlocks.Api;
using System.Text;
using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.Hr.Domain;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GiddyEdu.Infrastructure.Hr;

public sealed record PositionInput(string Name, StaffCategory Category = StaffCategory.Administrative);
public sealed record StaffInput(string? StaffNumber, string FirstName, string LastName, StaffCategory Category, Guid CampusId,
    Guid? DepartmentId, Guid? PositionId, string? Email, string? Phone, DateOnly HireDate, StaffStatus Status = StaffStatus.Active);
public sealed record StaffStatusInput(StaffStatus Status, DateOnly? ExitDate);
public sealed record StaffUserLinkInput(Guid UserId);
public sealed record StaffSensitiveInput(string? Address, string? NextOfKinName, string? NextOfKinPhone, string? Notes, string? Title = null, string? MiddleName = null,
    string? Gender = null, DateOnly? DateOfBirth = null, string? MaritalStatus = null, string? Religion = null, string? Country = null, string? State = null,
    string? LocalGovernment = null, string? City = null, string? Genotype = null, string? BloodGroup = null, decimal? WeightKg = null, decimal? HeightCm = null,
    string? Disability = null, string? Skills = null, string? Achievements = null, string? Website = null, string? OfficeAddress = null, string? SocialProfilesJson = null);
public sealed record PositionInfo(Guid Id, string Name, string Code, StaffCategory Category, string CategoryName, bool IsCustom, bool IsActive);
public sealed record StaffPositionInfo(Guid PositionId, string Name, StaffCategory Category, bool IsPrimary);
public sealed record StaffInfo(Guid Id, Guid? UserId, string StaffNumber, string FirstName, string LastName, StaffCategory Category,
    StaffStatus Status, Guid CampusId, Guid? DepartmentId, Guid? PositionId, string? WorkEmail, string? Phone, DateOnly HireDate, DateOnly? ExitDate,
    DateTimeOffset CreatedAtUtc, string? PositionName = null, string? MiddleInitial = null, string? PhotoUrl = null, DateTimeOffset? DeletedAtUtc = null);
public sealed record StaffAuditInfo(string Action, Guid? ActorUserId, DateTimeOffset OccurredAtUtc, string? ActorName = null);
public sealed record StaffBinDetail(StaffInfo Staff, Guid? DeletedByUserId, IReadOnlyList<StaffAuditInfo> Activity, string? DeletedByName = null);
public sealed record StaffSensitiveInfo(string? Address, string? NextOfKinName, string? NextOfKinPhone, string? Notes, string? Title, string? MiddleName, string? Gender,
    DateOnly? DateOfBirth, string? MaritalStatus, string? Religion, string? Country, string? State, string? LocalGovernment, string? City, string? Genotype,
    string? BloodGroup, decimal? WeightKg, decimal? HeightCm, string? Disability, string? Skills, string? Achievements, string? Website, string? OfficeAddress,
    string? SocialProfilesJson, DateTimeOffset UpdatedAtUtc);
public sealed record StaffEmploymentInput(string EmployerName, string JobTitle, DateOnly StartedOn, DateOnly? EndedOn, string? ReasonForLeaving);
public sealed record StaffEmploymentInfo(Guid Id, string EmployerName, string JobTitle, DateOnly StartedOn, DateOnly? EndedOn, string? ReasonForLeaving);
public sealed record StaffQualificationInput(string Institution, string Name, string? FieldOfStudy, DateOnly AwardedOn, string? Grade);
public sealed record StaffQualificationInfo(Guid Id, string Institution, string Name, string? FieldOfStudy, DateOnly AwardedOn, string? Grade);
public sealed record StaffNextOfKinInput(string FullName, string Relationship, string Phone, string? Email, string? Address, bool IsPrimary);
public sealed record StaffNextOfKinInfo(Guid Id, string FullName, string Relationship, string Phone, string? Email, string? Address, bool IsPrimary);
public sealed record StaffRoleAccessInfo(Guid MembershipId, Guid[] RoleIds);
public sealed record StaffExportInput(Guid[] StaffIds);
public sealed record TeachingAssignmentInput(Guid StaffId, Guid ClassSectionId, Guid? SubjectId, TeachingAssignmentRole Role);
public sealed record TeachingAssignmentInfo(Guid Id, Guid StaffId, Guid ClassSectionId, Guid? SubjectId, TeachingAssignmentRole Role);
public sealed class TeachingAssignmentConflictException(string message) : InvalidOperationException(message);

public interface IStaffService
{
    Task<IReadOnlyList<PositionInfo>> ListPositionsAsync(Guid actor, CancellationToken ct = default);
    Task<Guid> CreatePositionAsync(Guid actor, PositionInput input, CancellationToken ct = default);
    Task UpdatePositionAsync(Guid actor, Guid positionId, PositionInput input, CancellationToken ct = default);
    Task DeletePositionAsync(Guid actor, Guid positionId, CancellationToken ct = default);
    Task<PageResult<StaffInfo>> ListAsync(Guid actor, int page, int pageSize, string? search, StaffStatus? status, StaffCategory? category = null, string? sort = null, CancellationToken ct = default);
    Task<PageResult<StaffInfo>> ListBinAsync(Guid actor, int page, int pageSize, CancellationToken ct = default);
    Task<StaffBinDetail> GetBinDetailAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task<long> CountBinAsync(Guid actor, CancellationToken ct = default);
    Task MoveToBinAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task PermanentlyDeleteAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task<StaffInfo> GetAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StaffPositionInfo>> ListStaffPositionsAsync(Guid actor, Guid staffId, CancellationToken ct = default);
    Task AddStaffPositionAsync(Guid actor, Guid staffId, Guid positionId, CancellationToken ct = default);
    Task RemoveStaffPositionAsync(Guid actor, Guid staffId, Guid positionId, CancellationToken ct = default);
    Task SetPrimaryStaffPositionAsync(Guid actor, Guid staffId, Guid positionId, CancellationToken ct = default);
    Task<StaffRoleAccessInfo?> GetRoleAccessAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task<string> ExportSelectedAsync(Guid actor, StaffExportInput input, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid actor, StaffInput input, CancellationToken ct = default);
    Task UpdateAsync(Guid actor, Guid id, StaffInput input, CancellationToken ct = default);
    Task SetStatusAsync(Guid actor, Guid id, StaffStatusInput input, CancellationToken ct = default);
    Task LinkUserAsync(Guid actor, Guid id, StaffUserLinkInput input, CancellationToken ct = default);
    Task<StaffSensitiveInfo?> GetSensitiveAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task UpsertSensitiveAsync(Guid actor, Guid id, StaffSensitiveInput input, CancellationToken ct = default);
    Task<IReadOnlyList<StaffEmploymentInfo>> ListEmploymentAsync(Guid actor, Guid staffId, CancellationToken ct = default);
    Task<Guid> AddEmploymentAsync(Guid actor, Guid staffId, StaffEmploymentInput input, CancellationToken ct = default);
    Task DeleteEmploymentAsync(Guid actor, Guid staffId, Guid recordId, CancellationToken ct = default);
    Task<IReadOnlyList<StaffQualificationInfo>> ListQualificationsAsync(Guid actor, Guid staffId, CancellationToken ct = default);
    Task<Guid> AddQualificationAsync(Guid actor, Guid staffId, StaffQualificationInput input, CancellationToken ct = default);
    Task DeleteQualificationAsync(Guid actor, Guid staffId, Guid qualificationId, CancellationToken ct = default);
    Task<IReadOnlyList<StaffNextOfKinInfo>> ListNextOfKinAsync(Guid actor, Guid staffId, CancellationToken ct = default);
    Task<Guid> AddNextOfKinAsync(Guid actor, Guid staffId, StaffNextOfKinInput input, CancellationToken ct = default);
    Task UpdateNextOfKinAsync(Guid actor, Guid staffId, Guid contactId, StaffNextOfKinInput input, CancellationToken ct = default);
    Task DeleteNextOfKinAsync(Guid actor, Guid staffId, Guid contactId, CancellationToken ct = default);
    Task<IReadOnlyList<TeachingAssignmentInfo>> ListTeachingAssignmentsAsync(Guid actor, Guid? classSectionId, CancellationToken ct = default);
    Task<Guid> CreateTeachingAssignmentAsync(Guid actor, TeachingAssignmentInput input, CancellationToken ct = default);
    Task DeleteTeachingAssignmentAsync(Guid actor, Guid assignmentId, CancellationToken ct = default);
}

public sealed class StaffService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IPermissionService permissions, IClock clock, IFileObjectStorage storage) : IStaffService
{
    private sealed record StaffAccessSnapshot(Guid[] RoleIds, bool MembershipWasActive);
    private IQueryable<StaffProfile> Bin => db.StaffProfiles.IgnoreQueryFilters(["BinFilter"]).Where(x => x.DeletedAtUtc != null);

    public async Task<long> CountBinAsync(Guid actor, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        return await Bin.LongCountAsync(ct);
    }

    public async Task<PageResult<StaffInfo>> ListBinAsync(Guid actor, int page, int pageSize, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Bin.AsNoTracking();
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(x => x.DeletedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Project()).ToListAsync(ct);
        var positionIds = items.Where(x => x.PositionId.HasValue).Select(x => x.PositionId!.Value).Distinct().ToArray();
        var positions = await db.Positions.AsNoTracking().Where(x => positionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        items = items.Select(x => x with { PositionName = x.PositionId.HasValue && positions.TryGetValue(x.PositionId.Value, out var name) ? name : null }).ToList();
        return new(items, page, pageSize, total);
    }

    public async Task<StaffBinDetail> GetBinDetailAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var staff = await Bin.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Staff member was not found in the bin.");
        var positionName = staff.PositionId.HasValue ? await db.Positions.Where(x => x.Id == staff.PositionId.Value).Select(x => x.Name).SingleOrDefaultAsync(ct) : null;
        var activity = await db.AuditRecords.AsNoTracking().Where(x => x.TargetType == "StaffProfile" && x.TargetId == id.ToString())
            .OrderByDescending(x => x.OccurredAtUtc).Take(100)
            .Select(x => new StaffAuditInfo(x.Action, x.ActorUserId, x.OccurredAtUtc)).ToListAsync(ct);
        var actorIds = activity.Where(x => x.ActorUserId.HasValue).Select(x => x.ActorUserId!.Value)
            .Concat(staff.DeletedByUserId.HasValue ? [staff.DeletedByUserId.Value] : []).Distinct().ToArray();
        var actorNames = await db.Users.AsNoTracking().Where(x => actorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        activity = activity.Select(x => x with
        {
            ActorName = x.ActorUserId.HasValue && actorNames.TryGetValue(x.ActorUserId.Value, out var name)
                ? name : null
        }).ToList();
        var info = new StaffInfo(staff.Id, staff.UserId, staff.StaffNumber, staff.FirstName, staff.LastName, staff.Category, staff.Status,
            staff.CampusId, staff.DepartmentId, staff.PositionId, staff.WorkEmail, staff.Phone, staff.HireDate, staff.ExitDate,
            staff.CreatedAtUtc, positionName, null, null, staff.DeletedAtUtc);
        return new(info, staff.DeletedByUserId, activity,
            staff.DeletedByUserId.HasValue && actorNames.TryGetValue(staff.DeletedByUserId.Value, out var deletedByName)
                ? deletedByName : null);
    }

    public async Task MoveToBinAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var staff = await FindAsync(id, ct);
        if (staff.UserId == actor) throw new InvalidOperationException("You cannot move your own staff record to the bin.");
        if (staff.UserId.HasValue && !await permissions.HasPermissionAsync(actor, Permissions.RolesManage, ct))
            throw new UnauthorizedAccessException("Roles.Manage permission is required to suspend a linked staff account.");
        Guid[] suspendedRoles = [];
        var membershipWasActive = false;
        if (staff.UserId.HasValue)
        {
            var membership = await db.TenantMemberships.SingleOrDefaultAsync(x => x.UserId == staff.UserId.Value, ct);
            membershipWasActive = membership?.IsActive == true;
            var assignments = await (from tenantMembership in db.TenantMemberships
                                     join assignment in db.TenantMembershipRoles on tenantMembership.Id equals assignment.MembershipId
                                     join role in db.TenantRoles on assignment.RoleId equals role.Id
                                     where tenantMembership.UserId == staff.UserId.Value
                                     select new { Assignment = assignment, RoleName = role.Name }).ToListAsync(ct);
            var preservePersonRole = await db.Guardians.AnyAsync(x => x.UserId == staff.UserId.Value, ct)
                || await db.Students.AnyAsync(x => x.UserId == staff.UserId.Value, ct);
            var assignedRoleIds = assignments.Select(x => x.Assignment.RoleId).ToArray();
            var grants = await (from grant in db.RolePermissions
                                join permission in db.Permissions on grant.PermissionId equals permission.Id
                                where assignedRoleIds.Contains(grant.RoleId)
                                select new { grant.RoleId, permission.Name }).ToListAsync(ct);
            var familyPermissions = new HashSet<string>([Permissions.StudentsView, Permissions.GuardiansView, Permissions.AcademicsView]);
            if (preservePersonRole && assignments.Any(x => IsFamilyRole(x.RoleName) && grants.Any(grant => grant.RoleId == x.Assignment.RoleId && !familyPermissions.Contains(grant.Name))))
                throw new InvalidOperationException("A family role also grants staff or administrative access. Separate those permissions before moving this staff member to the bin.");
            var toRemove = assignments.Where(x => !preservePersonRole || !IsFamilyRole(x.RoleName) || grants.Any(grant => grant.RoleId == x.Assignment.RoleId && !familyPermissions.Contains(grant.Name)))
                .Select(x => x.Assignment).ToArray();
            suspendedRoles = toRemove.Select(x => x.RoleId).ToArray();
            db.TenantMembershipRoles.RemoveRange(toRemove);
            if (!preservePersonRole) membership?.Suspend();
            foreach (var token in await db.RefreshTokens.Where(x => x.UserId == staff.UserId.Value && x.RevokedAtUtc == null).ToListAsync(ct))
                token.Revoke(clock.UtcNow);
        }
        foreach (var invitation in await db.AccountInvitations.Where(x => x.TargetType == InvitationTargetType.Staff && x.TargetId == id && x.AcceptedAtUtc == null && x.RevokedAtUtc == null).ToListAsync(ct))
            invitation.Revoke(clock.UtcNow);
        staff.MoveToBin(actor, clock.UtcNow, JsonSerializer.Serialize(new StaffAccessSnapshot(suspendedRoles, membershipWasActive)));
        AddLifecycleAudit(actor, "Staff.MoveToBin", id);
        await db.SaveChangesAsync(ct);
    }

    public async Task RestoreAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var staff = await Bin.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Staff member was not found in the bin.");
        if (staff.UserId.HasValue && !await permissions.HasPermissionAsync(actor, Permissions.RolesManage, ct))
            throw new UnauthorizedAccessException("Roles.Manage permission is required to restore a linked staff account.");
        if (staff.UserId.HasValue)
        {
            var membership = await db.TenantMemberships.SingleOrDefaultAsync(x => x.UserId == staff.UserId.Value, ct);
            if (membership is null) throw new InvalidOperationException("The linked account no longer has a school membership.");
            var snapshot = ReadAccessSnapshot(staff.SuspendedRoleIdsJson);
            if (snapshot.MembershipWasActive) membership.Reactivate();
            var validRoleIds = await db.TenantRoles.Where(x => snapshot.RoleIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
            var existingRoleIds = await db.TenantMembershipRoles.Where(x => x.MembershipId == membership.Id).Select(x => x.RoleId).ToListAsync(ct);
            foreach (var roleId in validRoleIds.Except(existingRoleIds))
                db.TenantMembershipRoles.Add(new TenantMembershipRole(RequireTenant(), membership.Id, roleId));
        }
        staff.Restore(clock.UtcNow);
        AddLifecycleAudit(actor, "Staff.Restore", id);
        await db.SaveChangesAsync(ct);
    }

    public async Task PermanentlyDeleteAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var staff = await Bin.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Staff member was not found in the bin.");
        if (staff.UserId == actor) throw new InvalidOperationException("You cannot permanently delete your own staff record.");
        var files = await db.StoredFiles.Where(x => x.EntityType == "StaffProfile" && x.EntityId == id).ToListAsync(ct);
        foreach (var file in files) await storage.DeleteAsync(file.ObjectKey, ct);
        db.StoredFiles.RemoveRange(files);
        db.AccountInvitations.RemoveRange(await db.AccountInvitations.Where(x => x.TargetType == InvitationTargetType.Staff && x.TargetId == id).ToListAsync(ct));
        db.CustomFieldValues.RemoveRange(await db.CustomFieldValues.Where(x => x.EntityType == "StaffProfile" && x.EntityId == id).ToListAsync(ct));
        db.StaffProfiles.Remove(staff);
        AddLifecycleAudit(actor, "Staff.PermanentDelete", id);
        await db.SaveChangesAsync(ct);
    }

    private void AddLifecycleAudit(Guid actor, string action, Guid staffId) =>
        db.AuditRecords.Add(new AuditRecord(Guid.NewGuid(), RequireTenant(), actor, action, "StaffProfile", staffId.ToString(), "Succeeded", clock.UtcNow, null));

    private static bool IsFamilyRole(string name) =>
        name.Contains("parent", StringComparison.OrdinalIgnoreCase) || name.Contains("guardian", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("student", StringComparison.OrdinalIgnoreCase) || name.Contains("learner", StringComparison.OrdinalIgnoreCase);

    private static StaffAccessSnapshot ReadAccessSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new([], false);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? new(JsonSerializer.Deserialize<Guid[]>(json) ?? [], false)
            : JsonSerializer.Deserialize<StaffAccessSnapshot>(json) ?? new([], false);
    }

    public async Task<IReadOnlyList<PositionInfo>> ListPositionsAsync(Guid actor, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); await EnsureDefaultPositionsAsync(ct); return await db.Positions.AsNoTracking().OrderBy(x => x.Category).ThenBy(x => x.Name).Select(x => new PositionInfo(x.Id, x.Name, x.Code, x.Category, x.Category == StaffCategory.Teaching ? "Teaching" : x.Category == StaffCategory.Administrative ? "Administrative" : "Non-teaching", x.IsCustom, x.IsActive)).ToListAsync(ct); }

    public async Task<Guid> CreatePositionAsync(Guid actor, PositionInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var name = ValidatePositionName(input);
        if (await db.Positions.AnyAsync(x => x.Category == input.Category && x.Name.ToLower() == name.ToLower(), ct)) throw new InvalidOperationException("A position with this name already exists in the selected category.");
        var code = await GeneratePositionCodeAsync(name, ct);
        var id = Guid.NewGuid();
        db.Positions.Add(new Position(id, RequireTenant(), name, code, input.Category, true, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task UpdatePositionAsync(Guid actor, Guid positionId, PositionInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var name = ValidatePositionName(input);
        if (await db.Positions.AnyAsync(x => x.Id != positionId && x.Category == input.Category && x.Name.ToLower() == name.ToLower(), ct))
            throw new InvalidOperationException("A position with this name already exists.");
        var position = await db.Positions.SingleOrDefaultAsync(x => x.Id == positionId, ct)
            ?? throw new KeyNotFoundException("Position was not found.");
        if (!position.IsCustom) throw new InvalidOperationException("Default positions cannot be edited.");
        position.Update(name, input.Category);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeletePositionAsync(Guid actor, Guid positionId, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var position = await db.Positions.SingleOrDefaultAsync(x => x.Id == positionId, ct)
            ?? throw new KeyNotFoundException("Position was not found.");
        if (!position.IsCustom) throw new InvalidOperationException("Default positions cannot be deleted.");
        if (await db.StaffProfiles.AnyAsync(x => x.PositionId == positionId, ct) || await db.StaffAdditionalPositions.AnyAsync(x => x.PositionId == positionId, ct))
            throw new InvalidOperationException("This position is assigned to staff and cannot be deleted. Reassign those staff members first.");
        db.Positions.Remove(position);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PageResult<StaffInfo>> ListAsync(Guid actor, int page, int pageSize, string? search, StaffStatus? status, StaffCategory? category = null, string? sort = null, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StaffView, ct); RequireTenant(); page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.StaffProfiles.AsNoTracking().Where(x => (!status.HasValue || x.Status == status) && (!category.HasValue || x.Category == category));
        if (!await permissions.HasPermissionAsync(actor, Permissions.StaffManage, ct)) query = query.Where(x => x.UserId == actor);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => x.StaffNumber.Contains(term) || x.FirstName.Contains(term) || x.LastName.Contains(term) || (x.WorkEmail != null && x.WorkEmail.Contains(term)) || (x.Phone != null && x.Phone.Contains(term))); }
        var ordered = sort switch
        {
            "name_desc" => query.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName),
            "added_asc" => query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id),
            "added_desc" => query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id),
            "staff_number" => query.OrderBy(x => x.StaffNumber).ThenBy(x => x.Id),
            _ => query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
        };
        var total = await query.LongCountAsync(ct); var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(Project()).ToListAsync(ct);
        var ids = items.Select(x => x.Id).ToArray();
        var positionIds = items.Where(x => x.PositionId.HasValue).Select(x => x.PositionId!.Value).Distinct().ToArray();
        var positions = await db.Positions.AsNoTracking().Where(x => positionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var showSensitiveDetails = await permissions.HasPermissionAsync(actor, Permissions.StaffSensitiveView, ct);
        var middleNames = showSensitiveDetails
            ? await db.StaffSensitiveRecords.AsNoTracking().Where(x => ids.Contains(x.StaffId) && x.MiddleName != null).ToDictionaryAsync(x => x.StaffId, x => x.MiddleName!, ct)
            : new Dictionary<Guid, string>();
        var photos = showSensitiveDetails
            ? await db.StoredFiles.AsNoTracking().Where(x => ids.Contains(x.EntityId) && x.EntityType == "StaffProfile" && x.Category == "photo" && x.Status == GiddyEdu.Modules.Platform.Domain.StoredFileStatus.Available)
                .OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.EntityId, x.ObjectKey }).ToListAsync(ct)
            : [];
        var photoKeys = photos.GroupBy(x => x.EntityId).ToDictionary(group => group.Key, group => group.First().ObjectKey);
        items = items.Select(x => x with
        {
            PositionName = x.PositionId.HasValue && positions.TryGetValue(x.PositionId.Value, out var position) ? position : null,
            MiddleInitial = middleNames.TryGetValue(x.Id, out var middleName) && !string.IsNullOrWhiteSpace(middleName) ? middleName.Trim()[..1].ToUpperInvariant() : null,
            PhotoUrl = photoKeys.TryGetValue(x.Id, out var objectKey) ? storage.CreateDownloadUrl(objectKey) : null
        }).ToList();
        return new(items, page, pageSize, total);
    }

    public async Task<StaffInfo> GetAsync(Guid actor, Guid id, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); RequireTenant(); var query = db.StaffProfiles.AsNoTracking().Where(x => x.Id == id); if (!await permissions.HasPermissionAsync(actor, Permissions.StaffManage, ct)) query = query.Where(x => x.UserId == actor); return await query.Select(Project()).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Staff member was not found."); }

    public async Task<IReadOnlyList<StaffPositionInfo>> ListStaffPositionsAsync(Guid actor, Guid staffId, CancellationToken ct = default)
    {
        var staff = await GetAsync(actor, staffId, ct);
        var additional = await (from assignment in db.StaffAdditionalPositions.AsNoTracking()
                                join position in db.Positions.AsNoTracking() on assignment.PositionId equals position.Id
                                where assignment.StaffId == staffId
                                orderby position.Category, position.Name
                                select new StaffPositionInfo(position.Id, position.Name, position.Category, false)).ToListAsync(ct);
        if (staff.PositionId.HasValue)
        {
            var primary = await db.Positions.AsNoTracking().Where(x => x.Id == staff.PositionId.Value)
                .Select(x => new StaffPositionInfo(x.Id, x.Name, x.Category, true)).SingleAsync(ct);
            additional.Insert(0, primary);
        }
        return additional;
    }

    public async Task AddStaffPositionAsync(Guid actor, Guid staffId, Guid positionId, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var staff = await FindAsync(staffId, ct);
        if (staff.PositionId == positionId || await db.StaffAdditionalPositions.AnyAsync(x => x.StaffId == staffId && x.PositionId == positionId, ct))
            throw new InvalidOperationException("This position is already assigned to the staff member.");
        if (!await db.Positions.AnyAsync(x => x.Id == positionId && x.IsActive, ct))
            throw new InvalidOperationException("Choose an active position in this school.");
        db.StaffAdditionalPositions.Add(new StaffAdditionalPosition(RequireTenant(), staffId, positionId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveStaffPositionAsync(Guid actor, Guid staffId, Guid positionId, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        await FindAsync(staffId, ct);
        var assignment = await db.StaffAdditionalPositions.SingleOrDefaultAsync(x => x.StaffId == staffId && x.PositionId == positionId, ct)
            ?? throw new KeyNotFoundException("Additional position was not found. Change the primary position before removing it.");
        if (await HasTeachingAssignmentsAsync(staffId, ct) && await IsLastTeachingPositionAsync(staffId, positionId, ct))
            throw new InvalidOperationException("Remove teaching assignments before removing the last teaching position.");
        db.StaffAdditionalPositions.Remove(assignment);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetPrimaryStaffPositionAsync(Guid actor, Guid staffId, Guid positionId, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var staff = await FindAsync(staffId, ct);
        var position = await db.Positions.SingleOrDefaultAsync(x => x.Id == positionId && x.IsActive, ct)
            ?? throw new InvalidOperationException("Choose an active position in this school.");
        if (staff.PositionId == positionId) return;
        var existing = await db.StaffAdditionalPositions.SingleOrDefaultAsync(x => x.StaffId == staffId && x.PositionId == positionId, ct);
        if (existing is not null) db.StaffAdditionalPositions.Remove(existing);
        if (staff.PositionId.HasValue)
            db.StaffAdditionalPositions.Add(new StaffAdditionalPosition(RequireTenant(), staffId, staff.PositionId.Value, clock.UtcNow));
        staff.Update(staff.StaffNumber, staff.FirstName, staff.LastName, position.Category, staff.CampusId, staff.DepartmentId, positionId, staff.WorkEmail, staff.Phone, staff.HireDate, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<StaffRoleAccessInfo?> GetRoleAccessAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        if (!await permissions.HasPermissionAsync(actor, Permissions.RolesManage, ct)) throw new UnauthorizedAccessException("Roles.Manage permission is required.");
        var staff = await FindAsync(id, ct);
        if (!staff.UserId.HasValue) return null;
        var membership = await db.TenantMemberships.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == staff.UserId, ct);
        if (membership is null) return null;
        var roleIds = await db.TenantMembershipRoles.AsNoTracking().Where(x => x.MembershipId == membership.Id).Select(x => x.RoleId).ToArrayAsync(ct);
        return new StaffRoleAccessInfo(membership.Id, roleIds);
    }

    public async Task<string> ExportSelectedAsync(Guid actor, StaffExportInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var ids = input.StaffIds?.Distinct().ToArray() ?? [];
        if (ids.Length is < 1 or > 100 || ids.Contains(Guid.Empty)) throw new ArgumentException("Select between 1 and 100 staff records.");
        var staff = await db.StaffProfiles.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(x => new { x.StaffNumber, x.FirstName, x.LastName, x.WorkEmail, x.Phone, x.Category, x.Status, x.CreatedAtUtc }).ToListAsync(ct);
        if (staff.Count != ids.Length) throw new KeyNotFoundException("One or more staff records were not found in this school.");
        var csv = new StringBuilder("Staff ID,First name,Last name,Email,Phone,Category,Status,Added on (UTC)\r\n");
        foreach (var person in staff)
        {
            csv.AppendJoin(',', CsvCell(person.StaffNumber), CsvCell(person.FirstName), CsvCell(person.LastName), CsvCell(person.WorkEmail), CsvCell(person.Phone),
                CsvCell(person.Category.ToString()), CsvCell(person.Status.ToString()), CsvCell(person.CreatedAtUtc.ToString("O"))).Append("\r\n");
        }
        return csv.ToString();
    }

    public async Task<Guid> CreateAsync(Guid actor, StaffInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var campusId = await ResolveCampusAsync(input.CampusId, null, ct); await ValidateReferencesAsync(input, campusId, ct); ValidateContact(input); await EnsureUniqueContactAsync(input.Email!, input.Phone!, null, ct); var id = Guid.NewGuid(); var staffNumber = await GenerateStaffNumberAsync(ct); var staff = new StaffProfile(id, RequireTenant(), staffNumber, input.FirstName, input.LastName, input.Category, campusId, input.DepartmentId, input.PositionId, input.Email, input.Phone, input.HireDate, clock.UtcNow); staff.SetInitialStatus(input.Status, clock.UtcNow); db.StaffProfiles.Add(staff); await db.SaveChangesAsync(ct); return id; }

    public async Task UpdateAsync(Guid actor, Guid id, StaffInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct); var staff = await FindAsync(id, ct); var campusId = await ResolveCampusAsync(input.CampusId, staff.CampusId, ct);
        await ValidateReferencesAsync(input, campusId, ct); ValidateContact(input); await EnsureUniqueContactAsync(input.Email!, input.Phone!, id, ct);
        if (input.PositionId.HasValue && staff.PositionId != input.PositionId)
        {
            var promoted = await db.StaffAdditionalPositions.SingleOrDefaultAsync(x => x.StaffId == id && x.PositionId == input.PositionId, ct);
            if (promoted is not null) db.StaffAdditionalPositions.Remove(promoted);
            if (staff.PositionId.HasValue)
                db.StaffAdditionalPositions.Add(new StaffAdditionalPosition(RequireTenant(), id, staff.PositionId.Value, clock.UtcNow));
        }
        staff.Update(staff.StaffNumber, input.FirstName, input.LastName, input.Category, campusId, input.DepartmentId, input.PositionId, input.Email, input.Phone, input.HireDate, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(Guid actor, Guid id, StaffStatusInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct); var staff = await FindAsync(id, ct);
        switch (input.Status) { case StaffStatus.Exited when input.ExitDate.HasValue: staff.Exit(input.ExitDate.Value, clock.UtcNow); break; case StaffStatus.Exited: throw new ArgumentException("Exit date is required.", nameof(input)); default: staff.SetOperationalStatus(input.Status, clock.UtcNow); break; }
        await db.SaveChangesAsync(ct);
    }

    public async Task LinkUserAsync(Guid actor, Guid id, StaffUserLinkInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); if (!await db.TenantMemberships.AnyAsync(x => x.UserId == input.UserId && x.IsActive, ct)) throw new InvalidOperationException("User must have an active membership in the current tenant."); var staff = await FindAsync(id, ct); staff.LinkUser(input.UserId, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<StaffSensitiveInfo?> GetSensitiveAsync(Guid actor, Guid id, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffSensitiveView, ct); await EnsureStaffAsync(actor, id, ct); return await db.StaffSensitiveRecords.AsNoTracking().Where(x => x.StaffId == id).Select(x => new StaffSensitiveInfo(x.Address, x.NextOfKinName, x.NextOfKinPhone, x.Notes, x.Title, x.MiddleName, x.Gender, x.DateOfBirth, x.MaritalStatus, x.Religion, x.Country, x.State, x.LocalGovernment, x.City, x.Genotype, x.BloodGroup, x.WeightKg, x.HeightCm, x.Disability, x.Skills, x.Achievements, x.Website, x.OfficeAddress, x.SocialProfilesJson, x.UpdatedAtUtc)).SingleOrDefaultAsync(ct); }

    public async Task UpsertSensitiveAsync(Guid actor, Guid id, StaffSensitiveInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, id, ct); var record = await db.StaffSensitiveRecords.SingleOrDefaultAsync(x => x.StaffId == id, ct); if (record is null) db.StaffSensitiveRecords.Add(new StaffSensitiveRecord(RequireTenant(), id, input.Address, input.NextOfKinName, input.NextOfKinPhone, input.Notes, input.Title, input.MiddleName, input.Gender, input.DateOfBirth, input.MaritalStatus, input.Religion, input.Country, input.State, input.LocalGovernment, input.City, input.Genotype, input.BloodGroup, input.WeightKg, input.HeightCm, input.Disability, input.Skills, input.Achievements, input.Website, input.OfficeAddress, input.SocialProfilesJson, clock.UtcNow)); else record.Update(input.Address, input.NextOfKinName, input.NextOfKinPhone, input.Notes, input.Title, input.MiddleName, input.Gender, input.DateOfBirth, input.MaritalStatus, input.Religion, input.Country, input.State, input.LocalGovernment, input.City, input.Genotype, input.BloodGroup, input.WeightKg, input.HeightCm, input.Disability, input.Skills, input.Achievements, input.Website, input.OfficeAddress, input.SocialProfilesJson, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<StaffEmploymentInfo>> ListEmploymentAsync(Guid actor, Guid staffId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); await EnsureStaffAsync(actor, staffId, ct); return await db.StaffEmploymentRecords.AsNoTracking().Where(x => x.StaffId == staffId).OrderByDescending(x => x.StartedOn).Select(x => new StaffEmploymentInfo(x.Id, x.EmployerName, x.JobTitle, x.StartedOn, x.EndedOn, x.ReasonForLeaving)).ToListAsync(ct); }

    public async Task<Guid> AddEmploymentAsync(Guid actor, Guid staffId, StaffEmploymentInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var id = Guid.NewGuid(); db.StaffEmploymentRecords.Add(new StaffEmploymentRecord(id, RequireTenant(), staffId, input.EmployerName, input.JobTitle, input.StartedOn, input.EndedOn, input.ReasonForLeaving, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task DeleteEmploymentAsync(Guid actor, Guid staffId, Guid recordId, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var record = await db.StaffEmploymentRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.StaffId == staffId, ct) ?? throw new KeyNotFoundException("Employment record was not found."); db.Remove(record); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<StaffQualificationInfo>> ListQualificationsAsync(Guid actor, Guid staffId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); await EnsureStaffAsync(actor, staffId, ct); return await db.StaffQualifications.AsNoTracking().Where(x => x.StaffId == staffId).OrderByDescending(x => x.AwardedOn).Select(x => new StaffQualificationInfo(x.Id, x.Institution, x.Name, x.FieldOfStudy, x.AwardedOn, x.Grade)).ToListAsync(ct); }

    public async Task<Guid> AddQualificationAsync(Guid actor, Guid staffId, StaffQualificationInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var id = Guid.NewGuid(); db.StaffQualifications.Add(new StaffQualification(id, RequireTenant(), staffId, input.Institution, input.Name, input.FieldOfStudy, input.AwardedOn, input.Grade, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task DeleteQualificationAsync(Guid actor, Guid staffId, Guid qualificationId, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var record = await db.StaffQualifications.SingleOrDefaultAsync(x => x.Id == qualificationId && x.StaffId == staffId, ct) ?? throw new KeyNotFoundException("Qualification was not found."); db.Remove(record); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<StaffNextOfKinInfo>> ListNextOfKinAsync(Guid actor, Guid staffId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffSensitiveView, ct); await EnsureStaffAsync(actor, staffId, ct); return await db.StaffNextOfKinContacts.AsNoTracking().Where(x => x.StaffId == staffId).OrderByDescending(x => x.IsPrimary).ThenBy(x => x.FullName).Select(x => new StaffNextOfKinInfo(x.Id, x.FullName, x.Relationship, x.Phone, x.Email, x.Address, x.IsPrimary)).ToListAsync(ct); }

    public async Task<Guid> AddNextOfKinAsync(Guid actor, Guid staffId, StaffNextOfKinInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); if (input.IsPrimary) await ClearPrimaryAsync(staffId, null, ct); var id = Guid.NewGuid(); db.StaffNextOfKinContacts.Add(new StaffNextOfKin(id, RequireTenant(), staffId, input.FullName, input.Relationship, input.Phone, input.Email, input.Address, input.IsPrimary, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task UpdateNextOfKinAsync(Guid actor, Guid staffId, Guid contactId, StaffNextOfKinInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var contact = await db.StaffNextOfKinContacts.SingleOrDefaultAsync(x => x.Id == contactId && x.StaffId == staffId, ct) ?? throw new KeyNotFoundException("Next-of-kin contact was not found."); if (input.IsPrimary) await ClearPrimaryAsync(staffId, contactId, ct); contact.Update(input.FullName, input.Relationship, input.Phone, input.Email, input.Address, input.IsPrimary, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task DeleteNextOfKinAsync(Guid actor, Guid staffId, Guid contactId, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var contact = await db.StaffNextOfKinContacts.SingleOrDefaultAsync(x => x.Id == contactId && x.StaffId == staffId, ct) ?? throw new KeyNotFoundException("Next-of-kin contact was not found."); db.Remove(contact); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<TeachingAssignmentInfo>> ListTeachingAssignmentsAsync(Guid actor, Guid? classSectionId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); RequireTenant(); var query = db.TeachingAssignments.AsNoTracking().Where(x => !classSectionId.HasValue || x.ClassSectionId == classSectionId); if (!await permissions.HasPermissionAsync(actor, Permissions.StaffManage, ct)) query = query.Where(x => db.StaffProfiles.Any(staff => staff.Id == x.StaffId && staff.UserId == actor)); return await query.OrderBy(x => x.ClassSectionId).ThenBy(x => x.Role).Select(x => new TeachingAssignmentInfo(x.Id, x.StaffId, x.ClassSectionId, x.SubjectId, x.Role)).ToListAsync(ct); }

    public async Task<Guid> CreateTeachingAssignmentAsync(Guid actor, TeachingAssignmentInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var eligible = await db.StaffProfiles.AnyAsync(x => x.Id == input.StaffId && x.Status == StaffStatus.Active &&
            (x.Category == StaffCategory.Teaching || db.StaffAdditionalPositions.Any(a => a.StaffId == x.Id && db.Positions.Any(p => p.Id == a.PositionId && p.Category == StaffCategory.Teaching))), ct);
        if (!eligible) throw new InvalidOperationException("The staff member must be active and have a teaching position before receiving a class or subject assignment.");
        if (!await db.ClassSections.AnyAsync(x => x.Id == input.ClassSectionId && x.IsActive, ct)) throw new InvalidOperationException("The class section must belong to the current tenant and be active.");
        if (input.Role == TeachingAssignmentRole.SubjectTeacher && (!input.SubjectId.HasValue || !await db.ClassSubjects.AnyAsync(x => x.ClassSectionId == input.ClassSectionId && x.SubjectId == input.SubjectId, ct))) throw new InvalidOperationException("The subject must already be assigned to the class section.");
        var existing = await db.TeachingAssignments.AsNoTracking()
            .Where(x => x.ClassSectionId == input.ClassSectionId && x.Role == input.Role &&
                (input.Role == TeachingAssignmentRole.SubjectTeacher ? x.StaffId == input.StaffId && x.SubjectId == input.SubjectId : true))
            .Select(x => new { x.StaffId }).FirstOrDefaultAsync(ct);
        if (existing is not null)
            throw new TeachingAssignmentConflictException(existing.StaffId == input.StaffId
                ? "This staff member already has this class or subject assignment."
                : "This class already has a teacher for the selected responsibility.");
        var id = Guid.NewGuid();
        db.TeachingAssignments.Add(new TeachingAssignment(id, RequireTenant(), input.StaffId, input.ClassSectionId, input.SubjectId, input.Role, clock.UtcNow));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: { } constraint } && constraint.Contains("TeachingAssignments", StringComparison.Ordinal))
        { throw new TeachingAssignmentConflictException("This class or subject assignment already exists. Refresh the page to see the current assignments."); }
        return id;
    }

    public async Task DeleteTeachingAssignmentAsync(Guid actor, Guid assignmentId, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var assignment = await db.TeachingAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw new KeyNotFoundException("Teaching assignment was not found."); db.TeachingAssignments.Remove(assignment); await db.SaveChangesAsync(ct); }

    private Task<bool> HasTeachingAssignmentsAsync(Guid staffId, CancellationToken ct) =>
        db.TeachingAssignments.AnyAsync(x => x.StaffId == staffId, ct);

    private async Task<bool> IsLastTeachingPositionAsync(Guid staffId, Guid removingPositionId, CancellationToken ct)
    {
        var staff = await FindAsync(staffId, ct);
        if (staff.Category == StaffCategory.Teaching) return false;
        return !await db.StaffAdditionalPositions.AnyAsync(x => x.StaffId == staffId && x.PositionId != removingPositionId &&
            db.Positions.Any(p => p.Id == x.PositionId && p.Category == StaffCategory.Teaching), ct);
    }

    private async Task ValidateReferencesAsync(StaffInput input, Guid campusId, CancellationToken ct)
    {
        if (!await db.Campuses.AnyAsync(x => x.Id == campusId && x.IsActive, ct)) throw new InvalidOperationException("The active campus workspace must belong to the current tenant and be active.");
        if (input.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == input.DepartmentId && x.IsActive, ct)) throw new InvalidOperationException("Department must belong to the current tenant and be active.");
        if (input.PositionId.HasValue && !await db.Positions.AnyAsync(x => x.Id == input.PositionId && x.IsActive && x.Category == input.Category, ct)) throw new InvalidOperationException("Position must belong to the current tenant, be active, and match the staff category.");
    }
    private static void ValidateContact(StaffInput input)
    {
        if (StaffFieldNormalization.Phone(input.Phone) is null)
            throw new ArgumentException("Phone number must contain exactly 11 digits.", nameof(input));
        if (StaffFieldNormalization.Email(input.Email) is null)
            throw new ArgumentException("Enter a valid email address.", nameof(input));
    }
    private async Task EnsureUniqueContactAsync(string email, string phone, Guid? exceptStaffId, CancellationToken ct)
    {
        var normalizedEmail = StaffFieldNormalization.Email(email)!;
        var normalizedPhone = StaffFieldNormalization.Phone(phone)!;
        if (await db.StaffProfiles.AnyAsync(x => (!exceptStaffId.HasValue || x.Id != exceptStaffId) && x.WorkEmail != null && x.WorkEmail.ToLower() == normalizedEmail, ct))
            throw new InvalidOperationException("A staff record already exists with this email address.");
        if (await db.StaffProfiles.AnyAsync(x => (!exceptStaffId.HasValue || x.Id != exceptStaffId) && x.Phone == normalizedPhone, ct))
            throw new InvalidOperationException("A staff record already exists with this phone number.");
    }
    private async Task<Guid> ResolveCampusAsync(Guid requestedCampusId, Guid? existingCampusId, CancellationToken ct)
    {
        var campusId = tenant.CampusId ?? existingCampusId ?? (requestedCampusId == Guid.Empty ? null : requestedCampusId);
        if (!campusId.HasValue || campusId == Guid.Empty)
            throw new InvalidOperationException("Select an active campus workspace before creating or updating a staff record.");
        if (!await db.Campuses.AnyAsync(x => x.Id == campusId && x.IsActive, ct))
            throw new InvalidOperationException("The active campus workspace is unavailable.");
        return campusId.Value;
    }
    private async Task<string> GenerateStaffNumberAsync(CancellationToken ct)
    {
        var year = clock.UtcNow.Year;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = $"STF-{year}-{Guid.NewGuid():N}"[..17].ToUpperInvariant();
            if (!await db.StaffProfiles.AnyAsync(x => x.StaffNumber == candidate, ct)) return candidate;
        }
        throw new InvalidOperationException("A unique staff number could not be generated. Try again.");
    }
    private async Task<StaffProfile> FindAsync(Guid id, CancellationToken ct) => await db.StaffProfiles.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Staff member was not found.");
    private async Task EnsureStaffAsync(Guid actor, Guid id, CancellationToken ct) { RequireTenant(); var query = db.StaffProfiles.Where(x => x.Id == id); if (!await permissions.HasPermissionAsync(actor, Permissions.StaffManage, ct)) query = query.Where(x => x.UserId == actor); if (!await query.AnyAsync(ct)) throw new KeyNotFoundException("Staff member was not found."); }
    private async Task ClearPrimaryAsync(Guid staffId, Guid? exceptId, CancellationToken ct) { var contacts = await db.StaffNextOfKinContacts.Where(x => x.StaffId == staffId && x.IsPrimary && (!exceptId.HasValue || x.Id != exceptId)).ToListAsync(ct); foreach (var contact in contacts) contact.RemovePrimary(clock.UtcNow); }
    private async Task<string> GeneratePositionCodeAsync(string name, CancellationToken ct)
    {
        var baseCode = new string(string.Join('-', name.ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Select(character => char.IsAsciiLetterOrDigit(character) || character == '-' ? character : '-').ToArray());
        while (baseCode.Contains("--", StringComparison.Ordinal)) baseCode = baseCode.Replace("--", "-", StringComparison.Ordinal);
        baseCode = baseCode.Trim('-');
        if (string.IsNullOrWhiteSpace(baseCode)) throw new ArgumentException("Position name must contain letters or numbers.", nameof(name));
        baseCode = baseCode[..Math.Min(baseCode.Length, 40)].TrimEnd('-');
        var candidate = baseCode;
        for (var suffix = 2; await db.Positions.AnyAsync(x => x.Code == candidate, ct); suffix++)
            candidate = $"{baseCode[..Math.Min(baseCode.Length, 40 - suffix.ToString().Length - 1)]}-{suffix}";
        return candidate;
    }
    private static string ValidatePositionName(PositionInput input)
    {
        var name = string.Join(' ', (input.Name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return name.Length is 0 or > 150
            ? throw new ArgumentException("Position name is required and must not exceed 150 characters.", nameof(input))
            : name;
    }
    private async Task EnsureDefaultPositionsAsync(CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var defaults = new (StaffCategory Category, string Name)[] {
            (StaffCategory.Teaching,"Principal"),(StaffCategory.Teaching,"Vice Principal Academics"),(StaffCategory.Teaching,"Head Teacher"),(StaffCategory.Teaching,"Deputy Head Teacher"),(StaffCategory.Teaching,"Head of Department"),(StaffCategory.Teaching,"Class Teacher"),(StaffCategory.Teaching,"Subject Teacher"),(StaffCategory.Teaching,"Teaching Assistant"),(StaffCategory.Teaching,"Special Education Teacher"),(StaffCategory.Teaching,"Guidance Counsellor"),(StaffCategory.Teaching,"Librarian"),(StaffCategory.Teaching,"Laboratory Technician"),
            (StaffCategory.Administrative,"School Administrator"),(StaffCategory.Administrative,"Administrative Officer"),(StaffCategory.Administrative,"Registrar"),(StaffCategory.Administrative,"Admissions Officer"),(StaffCategory.Administrative,"Bursar"),(StaffCategory.Administrative,"Accountant"),(StaffCategory.Administrative,"Finance Officer"),(StaffCategory.Administrative,"Human Resources Officer"),(StaffCategory.Administrative,"ICT Officer"),(StaffCategory.Administrative,"Receptionist"),(StaffCategory.Administrative,"Storekeeper"),
            (StaffCategory.NonTeaching,"School Nurse"),(StaffCategory.NonTeaching,"Welfare Officer"),(StaffCategory.NonTeaching,"Boarding House Parent"),(StaffCategory.NonTeaching,"Driver"),(StaffCategory.NonTeaching,"Security Officer"),(StaffCategory.NonTeaching,"Cleaner"),(StaffCategory.NonTeaching,"Maintenance Officer"),(StaffCategory.NonTeaching,"Cook") };
        var existing = await db.Positions.Select(x => new { x.Category, x.Name }).ToListAsync(ct);
        foreach (var item in defaults.Where(item => !existing.Any(value => value.Category == item.Category && value.Name == item.Name)))
            db.Positions.Add(new Position(Guid.NewGuid(), tenantId, item.Name, await GeneratePositionCodeAsync(item.Name, ct), item.Category, false, clock.UtcNow));
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
    }
    private Task ManageAsync(Guid actor, CancellationToken ct) => DemandAsync(actor, Permissions.StaffManage, ct);
    private Task DemandAsync(Guid actor, string permission, CancellationToken ct) => access.DemandAsync(actor, permission, FeatureKeys.StaffManagement, ct);
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    private static string CsvCell(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && "=+-@\t\r".Contains(safe[0])) safe = "'" + safe;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
    private static System.Linq.Expressions.Expression<Func<StaffProfile, StaffInfo>> Project() => x => new StaffInfo(x.Id, x.UserId, x.StaffNumber, x.FirstName, x.LastName, x.Category, x.Status, x.CampusId, x.DepartmentId, x.PositionId, x.WorkEmail, x.Phone, x.HireDate, x.ExitDate, x.CreatedAtUtc, null, null, null, x.DeletedAtUtc);
}
