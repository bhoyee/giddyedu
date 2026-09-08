using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Identity;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GiddyEdu.SecurityTests;

public sealed class PlatformAdministratorBootstrapSecurityTests
{
    [Fact]
    public async Task Bootstrap_AssignsGlobalRoleOnce_ToExistingTenantMember()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformBootstrap:Enabled"] = "true",
            ["PlatformBootstrap:Email"] = "owner@giddyedu.test"
        }).Build();
        var tenantContext = new TenantContextAccessor();
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ITenantContext>(tenantContext)
            .AddDbContext<GiddyEduDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddIdentityCore<PlatformUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<GiddyEduDbContext>();
        await using var provider = services.BuildServiceProvider();
        var tenantId = Guid.NewGuid();
        tenantContext.Set(tenantId, null);
        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var owner = new PlatformUser { Id = Guid.NewGuid(), UserName = "owner@giddyedu.test", Email = "owner@giddyedu.test", EmailConfirmed = true, DisplayName = "Owner", CreatedAtUtc = DateTimeOffset.UtcNow };
            Assert.True((await users.CreateAsync(owner, "Correct-Horse-9!Battery")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<GiddyEduDbContext>();
            db.Tenants.Add(new Tenant(tenantId, "Owner School", "owner-school", DateTimeOffset.UtcNow));
            db.TenantMemberships.Add(new TenantMembership(Guid.NewGuid(), tenantId, owner.Id, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var bootstrap = new PlatformAdministratorBootstrap(provider.GetRequiredService<IServiceScopeFactory>(), configuration, NullLogger<PlatformAdministratorBootstrap>.Instance);
        await bootstrap.StartAsync(CancellationToken.None);
        await bootstrap.StartAsync(CancellationToken.None);

        await using var assertionScope = provider.CreateAsyncScope();
        var assertionUsers = assertionScope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var assigned = await assertionUsers.FindByEmailAsync("owner@giddyedu.test");
        Assert.NotNull(assigned);
        Assert.True(await assertionUsers.IsInRoleAsync(assigned, GlobalRoles.PlatformAdministrator));
    }
}
