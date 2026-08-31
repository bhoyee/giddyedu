using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Authorization;

public sealed class TenantRoleService(GiddyEduDbContext db, ITenantContext tenantContext, IPermissionService permissions)
{
    public async Task<Guid> CreateRoleAsync(Guid actorUserId, string name, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken = default)
    {
        await DemandManagementPermission(actorUserId, cancellationToken);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        var knownPermissions = await db.Permissions.Where(x => permissionIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken);
        if (knownPermissions.Count != permissionIds.Distinct().Count()) throw new InvalidOperationException("One or more permissions do not exist.");
        var role = new TenantRole(Guid.NewGuid(), tenantId, name);
        db.TenantRoles.Add(role);
        db.RolePermissions.AddRange(knownPermissions.Select(permissionId => new RolePermission(tenantId, role.Id, permissionId)));
        await db.SaveChangesAsync(cancellationToken);
        return role.Id;
    }

    public async Task AssignRoleAsync(Guid actorUserId, Guid membershipId, Guid roleId, CancellationToken cancellationToken = default)
    {
        await DemandManagementPermission(actorUserId, cancellationToken);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        if (!await db.TenantMemberships.AnyAsync(x => x.Id == membershipId && x.IsActive, cancellationToken) || !await db.TenantRoles.AnyAsync(x => x.Id == roleId, cancellationToken))
            throw new InvalidOperationException("Membership and role must belong to the current tenant.");
        db.TenantMembershipRoles.Add(new TenantMembershipRole(tenantId, membershipId, roleId));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task DemandManagementPermission(Guid actorUserId, CancellationToken cancellationToken)
    {
        if (!await permissions.HasPermissionAsync(actorUserId, Permissions.RolesManage, cancellationToken))
            throw new UnauthorizedAccessException("Roles.Manage is required.");
    }
}
