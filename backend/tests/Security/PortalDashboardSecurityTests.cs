using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Subscriptions;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.SecurityTests;

public sealed class PortalDashboardSecurityTests
{
    [Fact]
    public async Task AdministrativeMetrics_IncludeOnlyCurrentTenantRecords()
    {
        var context = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context);
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        db.Tenants.AddRange(new Tenant(tenantA, "A", $"a-{tenantA:N}", now), new Tenant(tenantB, "B", $"b-{tenantB:N}", now));
        await db.SaveChangesAsync();
        context.Set(tenantA, null); db.Campuses.Add(new Campus(Guid.NewGuid(), tenantA, "A Campus", "A", now)); await db.SaveChangesAsync();
        context.Set(tenantB, null); db.Campuses.Add(new Campus(Guid.NewGuid(), tenantB, "B Campus", "B", now)); await db.SaveChangesAsync();
        context.Set(tenantA, null); db.ChangeTracker.Clear();

        var service = new PortalDashboardService(db, new StubPermissions([Permissions.SchoolsView]), new StubProfile(["SchoolAdmin"]), new EnabledEntitlements());
        var dashboard = await service.GetAsync(Guid.NewGuid(), "SchoolAdmin");

        Assert.Equal(1, Assert.Single(dashboard.Metrics).Value);
    }

    [Fact]
    public async Task RequestedAudience_MustBelongToAuthenticatedUsersPresentation()
    {
        var context = new TenantContextAccessor(); context.Set(Guid.NewGuid(), null);
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context);
        var service = new PortalDashboardService(db, new StubPermissions([]), new StubProfile(["Parent"]), new EnabledEntitlements());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(Guid.NewGuid(), "SchoolAdmin"));
    }

    private sealed class StubPermissions(IReadOnlyCollection<string> values) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(values.Contains(permission));
        public Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(values);
    }

    private sealed class StubProfile(IReadOnlyList<string> audiences) : IAccessProfileService
    {
        public Task<AccessPresentation> GetPresentationAsync(Guid userId, IReadOnlyCollection<string> effectivePermissions, CancellationToken ct = default) =>
            Task.FromResult(new AccessPresentation([], audiences, audiences[0]));
    }

    private sealed class EnabledEntitlements : IEntitlementService
    {
        public Task<EffectiveEntitlement> GetAsync(string featureKey, Guid? campusId = null, CancellationToken cancellationToken = default) => Task.FromResult(new EffectiveEntitlement(true, null));
        public Task<bool> CanConsumeAsync(string featureKey, long quantity = 1, Guid? campusId = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RecordUsageAsync(string featureKey, long quantity = 1, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
