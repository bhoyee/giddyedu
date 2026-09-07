using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Authorization;

public sealed record PlatformTenantSummary(Guid Id, string Name, string Slug, bool IsActive, long Campuses, long Memberships, string? PlanName);
public interface IPlatformAdministrationService { Task<IReadOnlyList<PlatformTenantSummary>> ListTenantsAsync(Guid actor, CancellationToken ct = default); }

public sealed class PlatformAdministrationService(GiddyEduDbContext db) : IPlatformAdministrationService
{
    public async Task<IReadOnlyList<PlatformTenantSummary>> ListTenantsAsync(Guid actor, CancellationToken ct = default)
    {
        var authorised = await (from assignment in db.UserRoles join role in db.Roles on assignment.RoleId equals role.Id where assignment.UserId == actor && role.Name == GlobalRoles.PlatformAdministrator select role.Id).AnyAsync(ct);
        if (!authorised) throw new UnauthorizedAccessException("Platform administrator access is required.");
        return await db.Tenants.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Name).Select(tenant => new PlatformTenantSummary(
            tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive,
            db.Campuses.IgnoreQueryFilters().LongCount(x => x.TenantId == tenant.Id && x.IsActive),
            db.TenantMemberships.IgnoreQueryFilters().LongCount(x => x.TenantId == tenant.Id && x.IsActive),
            (from subscription in db.TenantSubscriptions.IgnoreQueryFilters() join plan in db.Plans on subscription.PlanId equals plan.Id where subscription.TenantId == tenant.Id && subscription.IsActive orderby subscription.StartsAtUtc descending select plan.Name).FirstOrDefault())).ToListAsync(ct);
    }
}
