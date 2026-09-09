using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Subscriptions;

public sealed record PlanInfo(Guid Id, string Code, string Name);
public sealed record SubscriptionInfo(Guid Id, Guid PlanId, string PlanCode, string PlanName, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc, DateTimeOffset? GraceEndsAtUtc, SubscriptionStatus Status, bool IsActive);
public sealed record SubscriptionRenewalInput(DateTimeOffset EndsAtUtc);
public sealed record SubscriptionGracePeriodInput(DateTimeOffset GraceEndsAtUtc);
public sealed record SubscriptionReactivationInput(DateTimeOffset EndsAtUtc);

public interface ISubscriptionManagementService
{
    Task<IReadOnlyList<PlanInfo>> ListPlansAsync(CancellationToken ct = default);
    Task<SubscriptionInfo?> GetCurrentAsync(Guid actorUserId, CancellationToken ct = default);
    Task<Guid> SubscribeAsync(Guid actorUserId, string planCode, CancellationToken ct = default);
    Task RenewAsync(Guid actorUserId, Guid subscriptionId, SubscriptionRenewalInput input, CancellationToken ct = default);
    Task BeginGracePeriodAsync(Guid actorUserId, Guid subscriptionId, SubscriptionGracePeriodInput input, CancellationToken ct = default);
    Task SuspendAsync(Guid actorUserId, Guid subscriptionId, CancellationToken ct = default);
    Task ReactivateAsync(Guid actorUserId, Guid subscriptionId, SubscriptionReactivationInput input, CancellationToken ct = default);
}

public sealed class SubscriptionManagementService(GiddyEduDbContext db, ITenantContext tenant, IPermissionService permissions, IClock clock) : ISubscriptionManagementService
{
    public async Task<IReadOnlyList<PlanInfo>> ListPlansAsync(CancellationToken ct = default) => await db.Plans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new PlanInfo(x.Id, x.Code, x.Name)).ToListAsync(ct);

    public async Task<SubscriptionInfo?> GetCurrentAsync(Guid actorUserId, CancellationToken ct = default)
    {
        await DemandManageAsync(actorUserId, ct); var tenantId = RequireTenant();
        var subscription = await db.TenantSubscriptions.Where(x => x.TenantId == tenantId).OrderByDescending(x => x.StartsAtUtc).FirstOrDefaultAsync(ct);
        if (subscription is null) return null;
        var effectivelyExpired = subscription.IsActive && ((subscription.Status == SubscriptionStatus.Active && subscription.EndsAtUtc <= clock.UtcNow) || (subscription.Status == SubscriptionStatus.GracePeriod && subscription.GraceEndsAtUtc <= clock.UtcNow));
        var plan = await db.Plans.AsNoTracking().SingleAsync(x => x.Id == subscription.PlanId, ct);
        return new SubscriptionInfo(subscription.Id, plan.Id, plan.Code, plan.Name, subscription.StartsAtUtc, subscription.EndsAtUtc, subscription.GraceEndsAtUtc, effectivelyExpired ? SubscriptionStatus.Expired : subscription.Status, subscription.IsActive && !effectivelyExpired);
    }

    public async Task<Guid> SubscribeAsync(Guid actorUserId, string planCode, CancellationToken ct = default)
    {
        await DemandManageAsync(actorUserId, ct); var tenantId = RequireTenant(); var normalized = planCode.Trim().ToLowerInvariant();
        var plan = await db.Plans.SingleOrDefaultAsync(x => x.Code == normalized && x.IsActive, ct) ?? throw new KeyNotFoundException("Subscription plan was not found.");
        if (await db.TenantSubscriptions.AnyAsync(x => x.IsActive, ct)) throw new InvalidOperationException("The tenant already has an active subscription.");
        var id = Guid.NewGuid(); db.TenantSubscriptions.Add(new TenantSubscription(id, tenantId, plan.Id, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task RenewAsync(Guid actorUserId, Guid subscriptionId, SubscriptionRenewalInput input, CancellationToken ct = default)
    { await DemandManageAsync(actorUserId, ct); var subscription = await FindAsync(subscriptionId, ct); subscription.Renew(input.EndsAtUtc, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task BeginGracePeriodAsync(Guid actorUserId, Guid subscriptionId, SubscriptionGracePeriodInput input, CancellationToken ct = default)
    { await DemandManageAsync(actorUserId, ct); var subscription = await FindAsync(subscriptionId, ct); subscription.BeginGracePeriod(input.GraceEndsAtUtc, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task SuspendAsync(Guid actorUserId, Guid subscriptionId, CancellationToken ct = default)
    { await DemandManageAsync(actorUserId, ct); var subscription = await FindAsync(subscriptionId, ct); subscription.Suspend(clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task ReactivateAsync(Guid actorUserId, Guid subscriptionId, SubscriptionReactivationInput input, CancellationToken ct = default)
    { await DemandManageAsync(actorUserId, ct); var subscription = await FindAsync(subscriptionId, ct); var now = clock.UtcNow; if (subscription.IsActive && ((subscription.Status == SubscriptionStatus.Active && subscription.EndsAtUtc <= now) || (subscription.Status == SubscriptionStatus.GracePeriod && subscription.GraceEndsAtUtc <= now))) subscription.Expire(now); subscription.Reactivate(input.EndsAtUtc, now); await db.SaveChangesAsync(ct); }

    private async Task DemandManageAsync(Guid actor, CancellationToken ct) { if (!await permissions.HasPermissionAsync(actor, Permissions.TenantSettingsManage, ct)) throw new UnauthorizedAccessException($"{Permissions.TenantSettingsManage} is required."); }
    private async Task<TenantSubscription> FindAsync(Guid id, CancellationToken ct) { RequireTenant(); return await db.TenantSubscriptions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Subscription was not found."); }
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
}
