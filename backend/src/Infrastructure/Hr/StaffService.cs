using GiddyEdu.BuildingBlocks.Api;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Hr.Domain;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Hr;

public sealed record PositionInput(string Name, string Code);
public sealed record StaffInput(string StaffNumber, string FirstName, string LastName, StaffCategory Category, Guid CampusId,
    Guid? DepartmentId, Guid? PositionId, string? WorkEmail, string? Phone, DateOnly HireDate);
public sealed record StaffStatusInput(StaffStatus Status, DateOnly? ExitDate);
public sealed record StaffUserLinkInput(Guid UserId);
public sealed record StaffSensitiveInput(string? Address, string? NextOfKinName, string? NextOfKinPhone, string? Notes);
public sealed record PositionInfo(Guid Id, string Name, string Code, bool IsActive);
public sealed record StaffInfo(Guid Id, Guid? UserId, string StaffNumber, string FirstName, string LastName, StaffCategory Category,
    StaffStatus Status, Guid CampusId, Guid? DepartmentId, Guid? PositionId, string? WorkEmail, string? Phone, DateOnly HireDate, DateOnly? ExitDate);
public sealed record StaffSensitiveInfo(string? Address, string? NextOfKinName, string? NextOfKinPhone, string? Notes, DateTimeOffset UpdatedAtUtc);
public sealed record TeachingAssignmentInput(Guid StaffId, Guid ClassSectionId, Guid? SubjectId, TeachingAssignmentRole Role);
public sealed record TeachingAssignmentInfo(Guid Id, Guid StaffId, Guid ClassSectionId, Guid? SubjectId, TeachingAssignmentRole Role);

public interface IStaffService
{
    Task<IReadOnlyList<PositionInfo>> ListPositionsAsync(Guid actor, CancellationToken ct = default);
    Task<Guid> CreatePositionAsync(Guid actor, PositionInput input, CancellationToken ct = default);
    Task<PageResult<StaffInfo>> ListAsync(Guid actor, int page, int pageSize, string? search, StaffStatus? status, CancellationToken ct = default);
    Task<StaffInfo> GetAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid actor, StaffInput input, CancellationToken ct = default);
    Task UpdateAsync(Guid actor, Guid id, StaffInput input, CancellationToken ct = default);
    Task SetStatusAsync(Guid actor, Guid id, StaffStatusInput input, CancellationToken ct = default);
    Task LinkUserAsync(Guid actor, Guid id, StaffUserLinkInput input, CancellationToken ct = default);
    Task<StaffSensitiveInfo?> GetSensitiveAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task UpsertSensitiveAsync(Guid actor, Guid id, StaffSensitiveInput input, CancellationToken ct = default);
    Task<IReadOnlyList<TeachingAssignmentInfo>> ListTeachingAssignmentsAsync(Guid actor, Guid? classSectionId, CancellationToken ct = default);
    Task<Guid> CreateTeachingAssignmentAsync(Guid actor, TeachingAssignmentInput input, CancellationToken ct = default);
    Task DeleteTeachingAssignmentAsync(Guid actor, Guid assignmentId, CancellationToken ct = default);
}

public sealed class StaffService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IClock clock) : IStaffService
{
    public async Task<IReadOnlyList<PositionInfo>> ListPositionsAsync(Guid actor, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); RequireTenant(); return await db.Positions.AsNoTracking().OrderBy(x => x.Name).Select(x => new PositionInfo(x.Id, x.Name, x.Code, x.IsActive)).ToListAsync(ct); }

    public async Task<Guid> CreatePositionAsync(Guid actor, PositionInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var id = Guid.NewGuid(); db.Positions.Add(new Position(id, RequireTenant(), input.Name, input.Code, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task<PageResult<StaffInfo>> ListAsync(Guid actor, int page, int pageSize, string? search, StaffStatus? status, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StaffView, ct); RequireTenant(); page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.StaffProfiles.AsNoTracking().Where(x => !status.HasValue || x.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => x.StaffNumber.Contains(term) || x.FirstName.Contains(term) || x.LastName.Contains(term)); }
        var total = await query.LongCountAsync(ct); var items = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Skip((page - 1) * pageSize).Take(pageSize).Select(Project()).ToListAsync(ct);
        return new(items, page, pageSize, total);
    }

    public async Task<StaffInfo> GetAsync(Guid actor, Guid id, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); RequireTenant(); return await db.StaffProfiles.AsNoTracking().Where(x => x.Id == id).Select(Project()).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Staff member was not found."); }

    public async Task<Guid> CreateAsync(Guid actor, StaffInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await ValidateReferencesAsync(input, ct); var id = Guid.NewGuid(); db.StaffProfiles.Add(new StaffProfile(id, RequireTenant(), input.StaffNumber, input.FirstName, input.LastName, input.Category, input.CampusId, input.DepartmentId, input.PositionId, input.WorkEmail, input.Phone, input.HireDate, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task UpdateAsync(Guid actor, Guid id, StaffInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await ValidateReferencesAsync(input, ct); var staff = await FindAsync(id, ct); staff.Update(input.StaffNumber, input.FirstName, input.LastName, input.Category, input.CampusId, input.DepartmentId, input.PositionId, input.WorkEmail, input.Phone, input.HireDate, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task SetStatusAsync(Guid actor, Guid id, StaffStatusInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct); var staff = await FindAsync(id, ct);
        switch (input.Status) { case StaffStatus.Active: staff.Reactivate(clock.UtcNow); break; case StaffStatus.Suspended: staff.Suspend(clock.UtcNow); break; case StaffStatus.Exited when input.ExitDate.HasValue: staff.Exit(input.ExitDate.Value, clock.UtcNow); break; case StaffStatus.Exited: throw new ArgumentException("Exit date is required.", nameof(input)); }
        await db.SaveChangesAsync(ct);
    }

    public async Task LinkUserAsync(Guid actor, Guid id, StaffUserLinkInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); if (!await db.TenantMemberships.AnyAsync(x => x.UserId == input.UserId && x.IsActive, ct)) throw new InvalidOperationException("User must have an active membership in the current tenant."); var staff = await FindAsync(id, ct); staff.LinkUser(input.UserId, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<StaffSensitiveInfo?> GetSensitiveAsync(Guid actor, Guid id, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffSensitiveView, ct); await EnsureStaffAsync(id, ct); return await db.StaffSensitiveRecords.AsNoTracking().Where(x => x.StaffId == id).Select(x => new StaffSensitiveInfo(x.Address, x.NextOfKinName, x.NextOfKinPhone, x.Notes, x.UpdatedAtUtc)).SingleOrDefaultAsync(ct); }

    public async Task UpsertSensitiveAsync(Guid actor, Guid id, StaffSensitiveInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(id, ct); var record = await db.StaffSensitiveRecords.SingleOrDefaultAsync(x => x.StaffId == id, ct); if (record is null) db.StaffSensitiveRecords.Add(new StaffSensitiveRecord(RequireTenant(), id, input.Address, input.NextOfKinName, input.NextOfKinPhone, input.Notes, clock.UtcNow)); else record.Update(input.Address, input.NextOfKinName, input.NextOfKinPhone, input.Notes, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<TeachingAssignmentInfo>> ListTeachingAssignmentsAsync(Guid actor, Guid? classSectionId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); RequireTenant(); return await db.TeachingAssignments.AsNoTracking().Where(x => !classSectionId.HasValue || x.ClassSectionId == classSectionId).OrderBy(x => x.ClassSectionId).ThenBy(x => x.Role).Select(x => new TeachingAssignmentInfo(x.Id, x.StaffId, x.ClassSectionId, x.SubjectId, x.Role)).ToListAsync(ct); }

    public async Task<Guid> CreateTeachingAssignmentAsync(Guid actor, TeachingAssignmentInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        if (!await db.StaffProfiles.AnyAsync(x => x.Id == input.StaffId && x.Category == StaffCategory.Teaching && x.Status == StaffStatus.Active, ct)) throw new InvalidOperationException("The assigned staff member must be active teaching staff in the current tenant.");
        if (!await db.ClassSections.AnyAsync(x => x.Id == input.ClassSectionId && x.IsActive, ct)) throw new InvalidOperationException("The class section must belong to the current tenant and be active.");
        if (input.Role == TeachingAssignmentRole.SubjectTeacher && (!input.SubjectId.HasValue || !await db.ClassSubjects.AnyAsync(x => x.ClassSectionId == input.ClassSectionId && x.SubjectId == input.SubjectId, ct))) throw new InvalidOperationException("The subject must already be assigned to the class section.");
        var id = Guid.NewGuid(); db.TeachingAssignments.Add(new TeachingAssignment(id, RequireTenant(), input.StaffId, input.ClassSectionId, input.SubjectId, input.Role, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task DeleteTeachingAssignmentAsync(Guid actor, Guid assignmentId, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var assignment = await db.TeachingAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw new KeyNotFoundException("Teaching assignment was not found."); db.TeachingAssignments.Remove(assignment); await db.SaveChangesAsync(ct); }

    private async Task ValidateReferencesAsync(StaffInput input, CancellationToken ct)
    {
        if (!await db.Campuses.AnyAsync(x => x.Id == input.CampusId && x.IsActive, ct)) throw new InvalidOperationException("Campus must belong to the current tenant and be active.");
        if (input.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == input.DepartmentId && x.IsActive, ct)) throw new InvalidOperationException("Department must belong to the current tenant and be active.");
        if (input.PositionId.HasValue && !await db.Positions.AnyAsync(x => x.Id == input.PositionId && x.IsActive, ct)) throw new InvalidOperationException("Position must belong to the current tenant and be active.");
    }
    private async Task<StaffProfile> FindAsync(Guid id, CancellationToken ct) => await db.StaffProfiles.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Staff member was not found.");
    private async Task EnsureStaffAsync(Guid id, CancellationToken ct) { RequireTenant(); if (!await db.StaffProfiles.AnyAsync(x => x.Id == id, ct)) throw new KeyNotFoundException("Staff member was not found."); }
    private Task ManageAsync(Guid actor, CancellationToken ct) => DemandAsync(actor, Permissions.StaffManage, ct);
    private Task DemandAsync(Guid actor, string permission, CancellationToken ct) => access.DemandAsync(actor, permission, FeatureKeys.StaffManagement, ct);
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    private static System.Linq.Expressions.Expression<Func<StaffProfile, StaffInfo>> Project() => x => new StaffInfo(x.Id, x.UserId, x.StaffNumber, x.FirstName, x.LastName, x.Category, x.Status, x.CampusId, x.DepartmentId, x.PositionId, x.WorkEmail, x.Phone, x.HireDate, x.ExitDate);
}
