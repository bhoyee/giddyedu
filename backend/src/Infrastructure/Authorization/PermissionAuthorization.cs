using System.Security.Claims;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Authorization;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default);
}

public sealed class PermissionService(GiddyEduDbContext dbContext, ITenantContext tenantContext) : IPermissionService
{
    public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        if (!tenantContext.TenantId.HasValue) return Task.FromResult(false);
        return (from membership in dbContext.TenantMemberships
                join assignment in dbContext.TenantMembershipRoles on membership.Id equals assignment.MembershipId
                join rolePermission in dbContext.RolePermissions on assignment.RoleId equals rolePermission.RoleId
                join grantedPermission in dbContext.Permissions on rolePermission.PermissionId equals grantedPermission.Id
                where membership.UserId == userId && membership.IsActive && grantedPermission.Name == permission
                select grantedPermission.Id).AnyAsync(cancellationToken);
    }
}

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(IPermissionService permissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var userId) && await permissions.HasPermissionAsync(userId, requirement.Permission))
            context.Succeed(requirement);
    }
}
