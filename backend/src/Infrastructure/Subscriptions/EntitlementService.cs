using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Modules.Subscriptions.Domain;

namespace GiddyEdu.Infrastructure.Subscriptions;

public sealed record EffectiveEntitlement(bool Enabled, long? Limit);

public interface IEntitlementService
{
    Task<EffectiveEntitlement> GetAsync(string featureKey, Guid? campusId = null, CancellationToken cancellationToken = default);
    Task<bool> CanConsumeAsync(string featureKey, long quantity = 1, Guid? campusId = null, CancellationToken cancellationToken = default);
    Task RecordUsageAsync(string featureKey, long quantity = 1, CancellationToken cancellationToken = default);
}

public sealed class EntitlementService(GiddyEduDbContext db, ITenantContext tenant, IClock clock) : IEntitlementService
{
    public async Task<EffectiveEntitlement> GetAsync(string featureKey, Guid? campusId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        var feature = await db.Features.SingleOrDefaultAsync(x => x.Key == featureKey, cancellationToken);
        if (feature is null) return new(false, null);
        var now = clock.UtcNow;
        var subscription = await db.TenantSubscriptions.Where(x => x.TenantId == tenantId && x.IsActive &&
            ((x.Status == SubscriptionStatus.Active && (!x.EndsAtUtc.HasValue || x.EndsAtUtc > now)) ||
             (x.Status == SubscriptionStatus.GracePeriod && x.GraceEndsAtUtc.HasValue && x.GraceEndsAtUtc > now)))
            .OrderByDescending(x => x.StartsAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (subscription is null) return new(false, null);
        var plan = await db.PlanEntitlements.SingleOrDefaultAsync(x => x.PlanId == subscription.PlanId && x.FeatureId == feature.Id, cancellationToken);
        var tenantOverride = await db.TenantEntitlementOverrides.SingleOrDefaultAsync(x => x.FeatureId == feature.Id, cancellationToken);
        var campusOverride = campusId.HasValue ? await db.CampusEntitlementOverrides.SingleOrDefaultAsync(x => x.CampusId == campusId && x.FeatureId == feature.Id, cancellationToken) : null;
        return new(campusOverride?.Enabled ?? tenantOverride?.Enabled ?? plan?.Enabled ?? false, campusOverride?.Limit ?? tenantOverride?.Limit ?? plan?.Limit);
    }

    public async Task<bool> CanConsumeAsync(string featureKey, long quantity = 1, Guid? campusId = null, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        var entitlement = await GetAsync(featureKey, campusId, cancellationToken); if (!entitlement.Enabled) return false; if (!entitlement.Limit.HasValue) return true;
        var featureId = await db.Features.Where(x => x.Key == featureKey).Select(x => x.Id).SingleAsync(cancellationToken);
        var period = new DateOnly(clock.UtcNow.Year, clock.UtcNow.Month, 1);
        var used = await db.FeatureUsage.Where(x => x.FeatureId == featureId && x.PeriodStart == period).Select(x => (long?)x.Quantity).SingleOrDefaultAsync(cancellationToken) ?? 0;
        return used <= entitlement.Limit.Value - quantity;
    }

    public async Task RecordUsageAsync(string featureKey, long quantity = 1, CancellationToken cancellationToken = default)
    {
        if (!await CanConsumeAsync(featureKey, quantity, null, cancellationToken)) throw new InvalidOperationException("The feature entitlement or usage limit does not allow this operation.");
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        var featureId = await db.Features.Where(x => x.Key == featureKey).Select(x => x.Id).SingleAsync(cancellationToken);
        var period = new DateOnly(clock.UtcNow.Year, clock.UtcNow.Month, 1);
        var usage = await db.FeatureUsage.SingleOrDefaultAsync(x => x.FeatureId == featureId && x.PeriodStart == period, cancellationToken);
        if (usage is null) db.FeatureUsage.Add(new FeatureUsage(Guid.NewGuid(), tenantId, featureId, period, quantity)); else usage.Add(quantity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
