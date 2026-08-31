using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Subscriptions;

public sealed record EffectiveEntitlement(bool Enabled, long? Limit);

public interface IEntitlementService
{
    Task<EffectiveEntitlement> GetAsync(string featureKey, Guid? campusId = null, CancellationToken cancellationToken = default);
}

public sealed class EntitlementService(GiddyEduDbContext db, ITenantContext tenant) : IEntitlementService
{
    public async Task<EffectiveEntitlement> GetAsync(string featureKey, Guid? campusId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        var feature = await db.Features.SingleOrDefaultAsync(x => x.Key == featureKey, cancellationToken);
        if (feature is null) return new(false, null);
        var subscription = await db.TenantSubscriptions.Where(x => x.TenantId == tenantId && x.IsActive).OrderByDescending(x => x.StartsAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (subscription is null) return new(false, null);
        var plan = await db.PlanEntitlements.SingleOrDefaultAsync(x => x.PlanId == subscription.PlanId && x.FeatureId == feature.Id, cancellationToken);
        var tenantOverride = await db.TenantEntitlementOverrides.SingleOrDefaultAsync(x => x.FeatureId == feature.Id, cancellationToken);
        var campusOverride = campusId.HasValue ? await db.CampusEntitlementOverrides.SingleOrDefaultAsync(x => x.CampusId == campusId && x.FeatureId == feature.Id, cancellationToken) : null;
        return new(campusOverride?.Enabled ?? tenantOverride?.Enabled ?? plan?.Enabled ?? false, campusOverride?.Limit ?? tenantOverride?.Limit ?? plan?.Limit);
    }
}
