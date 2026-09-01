using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Authorization;

public sealed record PermissionInfo(Guid Id, string Name, string Description);
public sealed record TenantRoleInfo(Guid Id, string Name, bool IsSystemTemplate, IReadOnlyCollection<PermissionInfo> Permissions);
public sealed record EffectivePermissionInfo(Guid MembershipId, Guid UserId, IReadOnlyCollection<string> Permissions);

public sealed class TenantRoleService(GiddyEduDbContext db, ITenantContext tenantContext, IPermissionService permissions)
{
    public async Task<IReadOnlyCollection<PermissionInfo>> ListPermissionsAsync(CancellationToken ct = default) =>
        await db.Permissions.AsNoTracking().OrderBy(x => x.Name).Select(x => new PermissionInfo(x.Id, x.Name, x.Description)).ToListAsync(ct);

    public async Task<IReadOnlyCollection<TenantRoleInfo>> ListRolesAsync(CancellationToken ct = default)
    {
        RequireTenant();
        var roles = await db.TenantRoles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var grants = await (from rp in db.RolePermissions join permission in db.Permissions on rp.PermissionId equals permission.Id
                            select new { rp.RoleId, Permission = new PermissionInfo(permission.Id, permission.Name, permission.Description) }).AsNoTracking().ToListAsync(ct);
        return roles.Select(role => new TenantRoleInfo(role.Id, role.Name, role.IsSystemTemplate,
            grants.Where(x => x.RoleId == role.Id).Select(x => x.Permission).OrderBy(x => x.Name).ToArray())).ToArray();
    }

    public async Task<Guid> CreateRoleAsync(Guid actorUserId, string name, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default)
    {
        ValidateRoleName(name); await DemandManagementPermission(actorUserId, ct); var tenantId = RequireTenant();
        var knownPermissions = await ValidateGrantablePermissionsAsync(actorUserId, permissionIds, ct);
        var role = new TenantRole(Guid.NewGuid(), tenantId, name); db.TenantRoles.Add(role);
        db.RolePermissions.AddRange(knownPermissions.Select(id => new RolePermission(tenantId, role.Id, id)));
        await db.SaveChangesAsync(ct); return role.Id;
    }

    public async Task RenameRoleAsync(Guid actorUserId, Guid roleId, string name, CancellationToken ct = default)
    {
        ValidateRoleName(name); await DemandManagementPermission(actorUserId, ct);
        var role = await db.TenantRoles.SingleOrDefaultAsync(x => x.Id == roleId, ct) ?? throw new KeyNotFoundException("Role was not found.");
        role.Rename(name); await db.SaveChangesAsync(ct);
    }

    public async Task ReplacePermissionsAsync(Guid actorUserId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default)
    {
        await DemandManagementPermission(actorUserId, ct); var tenantId = RequireTenant();
        _ = await db.TenantRoles.SingleOrDefaultAsync(x => x.Id == roleId, ct) ?? throw new KeyNotFoundException("Role was not found.");
        var knownPermissions = await ValidateGrantablePermissionsAsync(actorUserId, permissionIds, ct);
        var existing = await db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync(ct);
        var rolesManageId = await PermissionIdAsync(Permissions.RolesManage, ct);
        if (existing.Any(x => x.PermissionId == rolesManageId) && !knownPermissions.Contains(rolesManageId)) await EnsureAnotherRoleManagerExistsAsync(roleId, null, true, ct);
        db.RolePermissions.RemoveRange(existing.Where(x => !knownPermissions.Contains(x.PermissionId)));
        db.RolePermissions.AddRange(knownPermissions.Except(existing.Select(x => x.PermissionId)).Select(id => new RolePermission(tenantId, roleId, id)));
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteRoleAsync(Guid actorUserId, Guid roleId, CancellationToken ct = default)
    {
        await DemandManagementPermission(actorUserId, ct);
        var role = await db.TenantRoles.SingleOrDefaultAsync(x => x.Id == roleId, ct) ?? throw new KeyNotFoundException("Role was not found.");
        if (role.IsSystemTemplate) throw new InvalidOperationException("System template roles cannot be deleted.");
        var rolesManageId = await PermissionIdAsync(Permissions.RolesManage, ct);
        if (await db.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == rolesManageId, ct)) await EnsureAnotherRoleManagerExistsAsync(roleId, null, true, ct);
        db.TenantMembershipRoles.RemoveRange(await db.TenantMembershipRoles.Where(x => x.RoleId == roleId).ToListAsync(ct));
        db.TenantRoles.Remove(role); await db.SaveChangesAsync(ct);
    }

    public async Task AssignRoleAsync(Guid actorUserId, Guid membershipId, Guid roleId, CancellationToken ct = default)
    {
        await DemandManagementPermission(actorUserId, ct); var tenantId = RequireTenant();
        if (!await db.TenantMemberships.AnyAsync(x => x.Id == membershipId && x.IsActive, ct) || !await db.TenantRoles.AnyAsync(x => x.Id == roleId, ct))
            throw new InvalidOperationException("Membership and role must belong to the current tenant.");
        if (await db.TenantMembershipRoles.AnyAsync(x => x.MembershipId == membershipId && x.RoleId == roleId, ct)) return;
        db.TenantMembershipRoles.Add(new TenantMembershipRole(tenantId, membershipId, roleId)); await db.SaveChangesAsync(ct);
    }

    public async Task RemoveRoleAssignmentAsync(Guid actorUserId, Guid membershipId, Guid roleId, CancellationToken ct = default)
    {
        await DemandManagementPermission(actorUserId, ct);
        var assignment = await db.TenantMembershipRoles.SingleOrDefaultAsync(x => x.MembershipId == membershipId && x.RoleId == roleId, ct) ?? throw new KeyNotFoundException("Role assignment was not found.");
        var rolesManageId = await PermissionIdAsync(Permissions.RolesManage, ct);
        if (await db.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == rolesManageId, ct)) await EnsureAnotherRoleManagerExistsAsync(roleId, membershipId, false, ct);
        db.TenantMembershipRoles.Remove(assignment); await db.SaveChangesAsync(ct);
    }

    public async Task<EffectivePermissionInfo> GetEffectivePermissionsAsync(Guid actorUserId, Guid membershipId, CancellationToken ct = default)
    {
        await DemandManagementPermission(actorUserId, ct);
        var membership = await db.TenantMemberships.AsNoTracking().SingleOrDefaultAsync(x => x.Id == membershipId && x.IsActive, ct) ?? throw new KeyNotFoundException("Membership was not found.");
        var effective = await (from assignment in db.TenantMembershipRoles join grant in db.RolePermissions on assignment.RoleId equals grant.RoleId
                               join permission in db.Permissions on grant.PermissionId equals permission.Id where assignment.MembershipId == membershipId
                               select permission.Name).Distinct().OrderBy(x => x).ToListAsync(ct);
        return new EffectivePermissionInfo(membership.Id, membership.UserId, effective);
    }

    private async Task<HashSet<Guid>> ValidateGrantablePermissionsAsync(Guid actorUserId, IReadOnlyCollection<Guid> requestedIds, CancellationToken ct)
    {
        var requested = requestedIds.Distinct().ToHashSet();
        var known = await db.Permissions.Where(x => requested.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        if (known.Count != requested.Count) throw new ArgumentException("One or more permissions do not exist.", nameof(requestedIds));
        var actorPermissions = await EffectivePermissionIdsAsync(actorUserId, ct);
        if (!requested.IsSubsetOf(actorPermissions)) throw new UnauthorizedAccessException("A role manager cannot grant permissions they do not possess.");
        return requested;
    }

    private async Task<HashSet<Guid>> EffectivePermissionIdsAsync(Guid userId, CancellationToken ct) =>
        (await (from membership in db.TenantMemberships join assignment in db.TenantMembershipRoles on membership.Id equals assignment.MembershipId
                join grant in db.RolePermissions on assignment.RoleId equals grant.RoleId where membership.UserId == userId && membership.IsActive
                select grant.PermissionId).Distinct().ToListAsync(ct)).ToHashSet();

    private async Task EnsureAnotherRoleManagerExistsAsync(Guid? excludedRoleId, Guid? excludedMembershipId, bool excludeRoleGlobally, CancellationToken ct)
    {
        var permissionId = await PermissionIdAsync(Permissions.RolesManage, ct);
        var holders = await (from membership in db.TenantMemberships join assignment in db.TenantMembershipRoles on membership.Id equals assignment.MembershipId
                             join grant in db.RolePermissions on assignment.RoleId equals grant.RoleId
                             where membership.IsActive && grant.PermissionId == permissionId
                                   && !(excludeRoleGlobally && excludedRoleId.HasValue && assignment.RoleId == excludedRoleId.Value)
                                   && !(!excludeRoleGlobally && excludedRoleId.HasValue && excludedMembershipId.HasValue
                                        && assignment.RoleId == excludedRoleId.Value && assignment.MembershipId == excludedMembershipId.Value)
                             select membership.Id).Distinct().CountAsync(ct);
        if (holders == 0) throw new InvalidOperationException("The tenant must retain at least one active role manager.");
    }

    private async Task<Guid> PermissionIdAsync(string name, CancellationToken ct) => await db.Permissions.Where(x => x.Name == name).Select(x => x.Id).SingleAsync(ct);
    private async Task DemandManagementPermission(Guid actorUserId, CancellationToken ct) { if (!await permissions.HasPermissionAsync(actorUserId, Permissions.RolesManage, ct)) throw new UnauthorizedAccessException("Roles.Manage is required."); }
    private Guid RequireTenant() => tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    private static void ValidateRoleName(string name) { if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("Role name must contain between 1 and 100 characters.", nameof(name)); }
}
