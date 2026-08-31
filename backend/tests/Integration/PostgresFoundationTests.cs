using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Tenancy.Domain;
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

    private static GiddyEduDbContext CreateContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseNpgsql(ConnectionString).Options;
        return new GiddyEduDbContext(options, tenantContext);
    }
}
