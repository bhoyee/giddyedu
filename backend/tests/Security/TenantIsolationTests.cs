using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.SecurityTests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task TenantA_CannotReadTenantBRecords()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        await using var db = fixture.CreateContext();
        var campuses = await db.Campuses.ToListAsync();
        Assert.Single(campuses);
        Assert.Equal(fixture.TenantA, campuses[0].TenantId);
    }

    [Fact]
    public async Task TenantA_CannotUpdateOrDeleteTenantBRecords()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        await using (var updateDb = fixture.CreateContext())
        {
            var other = await updateDb.Campuses.IgnoreQueryFilters().SingleAsync(x => x.TenantId == fixture.TenantB);
            updateDb.Entry(other).State = EntityState.Modified;
            await Assert.ThrowsAsync<InvalidOperationException>(() => updateDb.SaveChangesAsync());
        }
        await using (var deleteDb = fixture.CreateContext())
        {
            var other = await deleteDb.Campuses.IgnoreQueryFilters().SingleAsync(x => x.TenantId == fixture.TenantB);
            deleteDb.Remove(other);
            await Assert.ThrowsAsync<InvalidOperationException>(() => deleteDb.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task TenantA_CannotAssignTenantBRoleOrUseTenantBCustomField()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        await using var db = fixture.CreateContext();
        db.TenantMembershipRoles.Add(new TenantMembershipRole(fixture.TenantB, Guid.NewGuid(), Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.CustomFieldValues.Add(new CustomFieldValue(Guid.NewGuid(), fixture.TenantB, Guid.NewGuid(), "Tenant", fixture.TenantB, "\"value\"", DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task AuditRecords_AreAppendOnly()
    {
        var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        await using var db = fixture.CreateContext();
        var record = new AuditRecord(Guid.NewGuid(), fixture.TenantA, null, "Test", "Tenant", fixture.TenantA.ToString(), "Succeeded", DateTimeOffset.UtcNow, null);
        db.AuditRecords.Add(record); await db.SaveChangesAsync();
        db.Remove(record);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private sealed class Fixture
    {
        private Fixture(DbContextOptions<GiddyEduDbContext> options, TenantContextAccessor context, Guid tenantA, Guid tenantB)
        { Options = options; Context = context; TenantA = tenantA; TenantB = tenantB; }
        public DbContextOptions<GiddyEduDbContext> Options { get; }
        public TenantContextAccessor Context { get; }
        public Guid TenantA { get; }
        public Guid TenantB { get; }
        public GiddyEduDbContext CreateContext() => new(Options, Context);

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var context = new TenantContextAccessor();
            var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid();
            await using var db = new GiddyEduDbContext(options, context);
            db.Tenants.AddRange(new Tenant(tenantA, "A", "a", DateTimeOffset.UtcNow), new Tenant(tenantB, "B", "b", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
            context.Set(tenantA, null); db.Campuses.Add(new Campus(Guid.NewGuid(), tenantA, "A Campus", "MAIN", DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
            context.Set(tenantB, null); db.Campuses.Add(new Campus(Guid.NewGuid(), tenantB, "B Campus", "MAIN", DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return new Fixture(options, context, tenantA, tenantB);
        }
    }
}
