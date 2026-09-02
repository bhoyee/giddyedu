using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Subscriptions;

public sealed record PlanInfo(Guid Id, string Code, string Name);
public sealed record SubscriptionInfo(Guid Id, Guid PlanId, string PlanCode, string PlanName, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc, bool IsActive);

public interface ISubscriptionManagementService
{
    Task<IReadOnlyList<PlanInfo>> ListPlansAsync(CancellationToken ct = default);
    Task<SubscriptionInfo?> GetCurrentAsync(Guid actorUserId, CancellationToken ct = default);
    Task<Guid> SubscribeAsync(Guid actorUserId, string planCode, CancellationToken ct = default);
}

public sealed class SubscriptionManagementService(GiddyEduDbContext db, ITenantContext tenant, IPermissionService permissions, IClock clock) : ISubscriptionManagementService
{
    public async Task<IReadOnlyList<PlanInfo>> ListPlansAsync(CancellationToken ct = default) => await db.Plans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new PlanInfo(x.Id, x.Code, x.Name)).ToListAsync(ct);

    public async Task<SubscriptionInfo?> GetCurrentAsync(Guid actorUserId, CancellationToken ct = default)
    {
        await DemandManageAsync(actorUserId, ct); var tenantId = RequireTenant();
        return await (from subscription in db.TenantSubscriptions.AsNoTracking() join plan in db.Plans on subscription.PlanId equals plan.Id where subscription.TenantId == tenantId && subscription.IsActive orderby subscription.StartsAtUtc descending select new SubscriptionInfo(subscription.Id, plan.Id, plan.Code, plan.Name, subscription.StartsAtUtc, subscription.EndsAtUtc, subscription.IsActive)).FirstOrDefaultAsync(ct);
    }

    public async Task<Guid> SubscribeAsync(Guid actorUserId, string planCode, CancellationToken ct = default)
    {
        await DemandManageAsync(actorUserId, ct); var tenantId = RequireTenant(); var normalized = planCode.Trim().ToLowerInvariant();
        var plan = await db.Plans.SingleOrDefaultAsync(x => x.Code == normalized && x.IsActive, ct) ?? throw new KeyNotFoundException("Subscription plan was not found.");
        if (await db.TenantSubscriptions.AnyAsync(x => x.IsActive, ct)) throw new InvalidOperationException("The tenant already has an active subscription.");
        var id = Guid.NewGuid(); db.TenantSubscriptions.Add(new TenantSubscription(id, tenantId, plan.Id, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    private async Task DemandManageAsync(Guid actor, CancellationToken ct) { if (!await permissions.HasPermissionAsync(actor, Permissions.TenantSettingsManage, ct)) throw new UnauthorizedAccessException($"{Permissions.TenantSettingsManage} is required."); }
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
}
