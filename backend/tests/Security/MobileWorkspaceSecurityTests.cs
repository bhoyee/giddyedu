using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.SecurityTests;

public sealed class MobileWorkspaceSecurityTests
{
    [Fact]
    public async Task WorkspaceDiscovery_ReturnsOnlyActiveMembershipsAndTheirCampuses()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var activeTenantId = Guid.NewGuid();
        var inactiveMembershipTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var activeCampusId = Guid.NewGuid();
        var context = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new GiddyEduDbContext(options, context);
        var now = DateTimeOffset.UtcNow;

        context.Set(activeTenantId, null);
        db.Tenants.Add(new Tenant(activeTenantId, "Active school", "active-school", now));
        db.Campuses.Add(new Campus(activeCampusId, activeTenantId, "Main campus", "MAIN", now));
        db.TenantMemberships.Add(new TenantMembership(Guid.NewGuid(), activeTenantId, userId, now));
        await db.SaveChangesAsync();

        context.Set(inactiveMembershipTenantId, null);
        db.Tenants.Add(new Tenant(inactiveMembershipTenantId, "Former school", "former-school", now));
        var inactiveMembership = new TenantMembership(Guid.NewGuid(), inactiveMembershipTenantId, userId, now);
        db.TenantMemberships.Add(inactiveMembership);
        db.Entry(inactiveMembership).Property(x => x.IsActive).CurrentValue = false;
        await db.SaveChangesAsync();

        context.Set(otherTenantId, null);
        db.Tenants.Add(new Tenant(otherTenantId, "Other school", "other-school", now));
        db.Campuses.Add(new Campus(Guid.NewGuid(), otherTenantId, "Other campus", "OTHER", now));
        db.TenantMemberships.Add(new TenantMembership(Guid.NewGuid(), otherTenantId, otherUserId, now));
        await db.SaveChangesAsync();

        var workspaces = await AuthEndpoints.LoadWorkspacesAsync(userId, db, CancellationToken.None);

        var workspace = Assert.Single(workspaces);
        Assert.Equal(activeTenantId, workspace.TenantId);
        Assert.Equal(activeCampusId, Assert.Single(workspace.Campuses).CampusId);
    }
}
