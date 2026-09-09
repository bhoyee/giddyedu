using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Authorization;

public sealed record PlatformTenantSummary(Guid Id, string Name, string Slug, bool IsActive, long Campuses, long Memberships,
    Guid? SubscriptionId, string? PlanCode, string? PlanName, SubscriptionStatus? SubscriptionStatus, DateTimeOffset? SubscriptionEndsAtUtc, DateTimeOffset? GraceEndsAtUtc);
public sealed record PlatformTenantStatusInput(bool IsActive);
public sealed record PlatformSubscriptionInput(string PlanCode, DateTimeOffset EndsAtUtc);
public sealed record PlatformGracePeriodInput(DateTimeOffset GraceEndsAtUtc);

public interface IPlatformAdministrationService
{
    Task<IReadOnlyList<PlatformTenantSummary>> ListTenantsAsync(Guid actor, CancellationToken ct = default);
    Task SetTenantStatusAsync(Guid actor, Guid tenantId, PlatformTenantStatusInput input, CancellationToken ct = default);
    Task<Guid> ProvisionSubscriptionAsync(Guid actor, Guid tenantId, PlatformSubscriptionInput input, CancellationToken ct = default);
    Task BeginGracePeriodAsync(Guid actor, Guid tenantId, Guid subscriptionId, PlatformGracePeriodInput input, CancellationToken ct = default);
    Task SuspendSubscriptionAsync(Guid actor, Guid tenantId, Guid subscriptionId, CancellationToken ct = default);
    Task ReactivateSubscriptionAsync(Guid actor, Guid tenantId, Guid subscriptionId, PlatformSubscriptionInput input, CancellationToken ct = default);
}

public sealed class PlatformAdministrationService(GiddyEduDbContext db, ITenantContextSetter tenantContext, IClock clock) : IPlatformAdministrationService
{
    public async Task<IReadOnlyList<PlatformTenantSummary>> ListTenantsAsync(Guid actor, CancellationToken ct = default)
    {
        await DemandPlatformAdministratorAsync(actor, ct);
        return await db.Tenants.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Name).Select(tenant => new PlatformTenantSummary(
            tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive,
            db.Campuses.IgnoreQueryFilters().LongCount(x => x.TenantId == tenant.Id && x.IsActive),
            db.TenantMemberships.IgnoreQueryFilters().LongCount(x => x.TenantId == tenant.Id && x.IsActive),
            db.TenantSubscriptions.IgnoreQueryFilters().Where(x => x.TenantId == tenant.Id).OrderByDescending(x => x.StartsAtUtc).Select(x => (Guid?)x.Id).FirstOrDefault(),
            (from subscription in db.TenantSubscriptions.IgnoreQueryFilters() join plan in db.Plans on subscription.PlanId equals plan.Id where subscription.TenantId == tenant.Id orderby subscription.StartsAtUtc descending select plan.Code).FirstOrDefault(),
            (from subscription in db.TenantSubscriptions.IgnoreQueryFilters() join plan in db.Plans on subscription.PlanId equals plan.Id where subscription.TenantId == tenant.Id orderby subscription.StartsAtUtc descending select plan.Name).FirstOrDefault(),
            db.TenantSubscriptions.IgnoreQueryFilters().Where(x => x.TenantId == tenant.Id).OrderByDescending(x => x.StartsAtUtc).Select(x => (SubscriptionStatus?)x.Status).FirstOrDefault(),
            db.TenantSubscriptions.IgnoreQueryFilters().Where(x => x.TenantId == tenant.Id).OrderByDescending(x => x.StartsAtUtc).Select(x => x.EndsAtUtc).FirstOrDefault(),
            db.TenantSubscriptions.IgnoreQueryFilters().Where(x => x.TenantId == tenant.Id).OrderByDescending(x => x.StartsAtUtc).Select(x => x.GraceEndsAtUtc).FirstOrDefault())).ToListAsync(ct);
    }

    public async Task SetTenantStatusAsync(Guid actor, Guid tenantId, PlatformTenantStatusInput input, CancellationToken ct = default)
    {
        await DemandPlatformAdministratorAsync(actor, ct);
        var target = await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == tenantId, ct) ?? throw new KeyNotFoundException("Tenant was not found.");
        await InTenantScopeAsync(tenantId, async () => { if (input.IsActive) target.Reactivate(clock.UtcNow); else target.Suspend(clock.UtcNow); AddAudit(actor, input.IsActive ? "Tenant.Reactivate" : "Tenant.Suspend", "Tenant", tenantId, null); await db.SaveChangesAsync(ct); });
    }

    public async Task<Guid> ProvisionSubscriptionAsync(Guid actor, Guid tenantId, PlatformSubscriptionInput input, CancellationToken ct = default)
    {
        await DemandPlatformAdministratorAsync(actor, ct); await EnsureTenantAsync(tenantId, ct);
        var planCode = input.PlanCode.Trim().ToLowerInvariant();
        var plan = await db.Plans.SingleOrDefaultAsync(x => x.Code == planCode && x.IsActive, ct) ?? throw new KeyNotFoundException("Subscription plan was not found.");
        return await InTenantScopeAsync(tenantId, async () => {
            var subscription = await db.TenantSubscriptions.OrderByDescending(x => x.StartsAtUtc).FirstOrDefaultAsync(ct);
            if (subscription is null || subscription.Status == SubscriptionStatus.Cancelled) { subscription = new TenantSubscription(Guid.NewGuid(), tenantId, plan.Id, clock.UtcNow, input.EndsAtUtc); db.TenantSubscriptions.Add(subscription); }
            else { subscription.ChangePlan(plan.Id, clock.UtcNow); subscription.Renew(input.EndsAtUtc, clock.UtcNow); }
            AddAudit(actor, "Subscription.Provision", "TenantSubscription", subscription.Id, new { tenantId, planCode, input.EndsAtUtc }); await db.SaveChangesAsync(ct); return subscription.Id;
        });
    }

    public async Task BeginGracePeriodAsync(Guid actor, Guid tenantId, Guid subscriptionId, PlatformGracePeriodInput input, CancellationToken ct = default)
    { await ChangeSubscriptionAsync(actor, tenantId, subscriptionId, "Subscription.GracePeriodBegin", subscription => subscription.BeginGracePeriod(input.GraceEndsAtUtc, clock.UtcNow), new { input.GraceEndsAtUtc }, ct); }
    public async Task SuspendSubscriptionAsync(Guid actor, Guid tenantId, Guid subscriptionId, CancellationToken ct = default)
    { await ChangeSubscriptionAsync(actor, tenantId, subscriptionId, "Subscription.Suspend", subscription => subscription.Suspend(clock.UtcNow), null, ct); }
    public async Task ReactivateSubscriptionAsync(Guid actor, Guid tenantId, Guid subscriptionId, PlatformSubscriptionInput input, CancellationToken ct = default)
    {
        await DemandPlatformAdministratorAsync(actor, ct); var planCode = input.PlanCode.Trim().ToLowerInvariant();
        var plan = await db.Plans.SingleOrDefaultAsync(x => x.Code == planCode && x.IsActive, ct) ?? throw new KeyNotFoundException("Subscription plan was not found.");
        await ChangeSubscriptionAsync(actor, tenantId, subscriptionId, "Subscription.Reactivate", subscription => { subscription.ChangePlan(plan.Id, clock.UtcNow); if (subscription.IsActive) subscription.Suspend(clock.UtcNow); subscription.Reactivate(input.EndsAtUtc, clock.UtcNow); }, new { planCode, input.EndsAtUtc }, ct, false);
    }

    private async Task ChangeSubscriptionAsync(Guid actor, Guid tenantId, Guid subscriptionId, string action, Action<TenantSubscription> change, object? metadata, CancellationToken ct, bool authorise = true)
    {
        if (authorise) await DemandPlatformAdministratorAsync(actor, ct); await EnsureTenantAsync(tenantId, ct);
        await InTenantScopeAsync(tenantId, async () => { var subscription = await db.TenantSubscriptions.SingleOrDefaultAsync(x => x.Id == subscriptionId, ct) ?? throw new KeyNotFoundException("Subscription was not found for this tenant."); change(subscription); AddAudit(actor, action, "TenantSubscription", subscriptionId, metadata); await db.SaveChangesAsync(ct); });
    }
    private async Task EnsureTenantAsync(Guid tenantId, CancellationToken ct) { if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id == tenantId, ct)) throw new KeyNotFoundException("Tenant was not found."); }
    private async Task DemandPlatformAdministratorAsync(Guid actor, CancellationToken ct) { var authorised = await (from assignment in db.UserRoles join role in db.Roles on assignment.RoleId equals role.Id where assignment.UserId == actor && role.Name == GlobalRoles.PlatformAdministrator select role.Id).AnyAsync(ct); if (!authorised) throw new UnauthorizedAccessException("Platform administrator access is required."); }
    private void AddAudit(Guid actor, string action, string targetType, Guid targetId, object? metadata) => db.PlatformAuditRecords.Add(new PlatformAuditRecord(Guid.NewGuid(), actor, action, targetType, targetId.ToString(), "Succeeded", clock.UtcNow, metadata is null ? null : JsonSerializer.Serialize(metadata)));
    private async Task InTenantScopeAsync(Guid tenantId, Func<Task> action) { tenantContext.Set(tenantId, null); try { await action(); } finally { tenantContext.Clear(); } }
    private async Task<T> InTenantScopeAsync<T>(Guid tenantId, Func<Task<T>> action) { tenantContext.Set(tenantId, null); try { return await action(); } finally { tenantContext.Clear(); } }
}
