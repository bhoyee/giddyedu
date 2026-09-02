using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Tenancy.Domain;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.IntegrationTests;

public sealed class PostgresFoundationTests
{
    private static readonly string ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
        ?? "Host=localhost;Port=5432;Database=giddyedu;Username=giddyedu;Password=giddyedu_local_development";

    [Fact]
    public async Task Database_IsReachableAndPhaseZeroMigrationIsApplied()
    {
        var tenantContext = new TenantContextAccessor();
        await using var db = CreateContext(tenantContext);
        Assert.True(await db.Database.CanConnectAsync());
        var pending = await db.Database.GetPendingMigrationsAsync();
        Assert.Empty(pending);
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), migration => migration.EndsWith("_Phase0Foundation", StringComparison.Ordinal));
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), migration => migration.EndsWith("_Phase1SchoolAcademicCore", StringComparison.Ordinal));
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), migration => migration.EndsWith("_Phase1PermissionCatalog", StringComparison.Ordinal));
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), migration => migration.EndsWith("_Phase1AcademicIntegrity", StringComparison.Ordinal));
        Assert.Equal(4, await db.Permissions.CountAsync(x => x.Name == "Schools.View" || x.Name == "Schools.Manage" || x.Name == "Academics.View" || x.Name == "Academics.Manage"));
    }

    [Fact]
    public async Task PostgreSqlQueryFilters_IsolateTenantRecords()
    {
        var tenantContext = new TenantContextAccessor();
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        db.Tenants.AddRange(new Tenant(tenantA, "Integration A", $"integration-a-{tenantA:N}", DateTimeOffset.UtcNow), new Tenant(tenantB, "Integration B", $"integration-b-{tenantB:N}", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        tenantContext.Set(tenantA, null);
        db.Campuses.Add(new Campus(Guid.NewGuid(), tenantA, "A Campus", "MAIN", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        tenantContext.Set(tenantB, null);
        db.Campuses.Add(new Campus(Guid.NewGuid(), tenantB, "B Campus", "MAIN", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        tenantContext.Set(tenantA, null);
        var visible = await db.Campuses.AsNoTracking().ToListAsync();
        Assert.Single(visible);
        Assert.Equal(tenantA, visible[0].TenantId);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task PostgreSqlCompositeKeys_RejectCrossTenantRoleAndCustomFieldReferences()
    {
        var context = new TenantContextAccessor(); await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync();
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid(); var roleB = Guid.NewGuid(); var membershipA = Guid.NewGuid(); var definitionB = Guid.NewGuid();
        var user = new PlatformUser { Id = Guid.NewGuid(), UserName = $"integration-{Guid.NewGuid():N}@example.com", DisplayName = "Integration User", CreatedAtUtc = DateTimeOffset.UtcNow };
        db.Users.Add(user); db.Tenants.AddRange(new Tenant(tenantA, "Constraint A", $"constraint-a-{tenantA:N}", DateTimeOffset.UtcNow), new Tenant(tenantB, "Constraint B", $"constraint-b-{tenantB:N}", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(); context.Set(tenantA, null); db.TenantMemberships.Add(new TenantMembership(membershipA, tenantA, user.Id, DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
        context.Set(tenantB, null); db.TenantRoles.Add(new TenantRole(roleB, tenantB, "B role")); db.CustomFieldDefinitions.Add(new CustomFieldDefinition(definitionB, tenantB, "Tenancy", "Campus", "integration_field", "Integration", CustomFieldDataType.ShortText, DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); context.Set(tenantA, null);
        db.TenantMembershipRoles.Add(new TenantMembershipRole(tenantA, membershipA, roleB));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.CustomFieldValues.Add(new CustomFieldValue(Guid.NewGuid(), tenantA, definitionB, "Campus", Guid.NewGuid(), "\"value\"", DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await transaction.RollbackAsync();
    }

    private static GiddyEduDbContext CreateContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public")).Options;
        return new GiddyEduDbContext(options, tenantContext);
    }
}
