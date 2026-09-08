using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Identity;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Tenancy;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace GiddyEdu.SecurityTests;

public sealed class PlatformAdministratorBootstrapSecurityTests
{
    [Fact]
    public async Task TenantMiddleware_AllowsTenantlessPlatformRole_ButRejectsOrdinaryIdentity()
    {
        var tenantContext = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, tenantContext);
        var invoked = false;
        var middleware = new TenantResolutionMiddleware(_ => { invoked = true; return Task.CompletedTask; });
        var platformRequest = AuthenticatedContext(new Claim(ClaimTypes.Role, GlobalRoles.PlatformAdministrator));

        await middleware.InvokeAsync(platformRequest, tenantContext, db);

        Assert.True(invoked);
        var ordinaryRequest = AuthenticatedContext();
        await middleware.InvokeAsync(ordinaryRequest, tenantContext, db);
        Assert.Equal(StatusCodes.Status403Forbidden, ordinaryRequest.Response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_CreatesTenantlessPlatformOperator_AndAuditsAssignmentOnce()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformBootstrap:Enabled"] = "true",
            ["PlatformBootstrap:Email"] = "owner@giddyedu.test",
            ["PlatformBootstrap:DisplayName"] = "Platform Owner"
        }).Build();
        var tenantContext = new TenantContextAccessor();
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ITenantContext>(tenantContext)
            .AddSingleton<IClock, SystemClock>()
            .AddDbContext<GiddyEduDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddIdentityCore<PlatformUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<GiddyEduDbContext>();
        await using var provider = services.BuildServiceProvider();
        var bootstrap = new PlatformAdministratorBootstrap(provider.GetRequiredService<IServiceScopeFactory>(), configuration, NullLogger<PlatformAdministratorBootstrap>.Instance);
        await bootstrap.StartAsync(CancellationToken.None);
        await bootstrap.StartAsync(CancellationToken.None);

        await using var assertionScope = provider.CreateAsyncScope();
        var assertionUsers = assertionScope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var assigned = await assertionUsers.FindByEmailAsync("owner@giddyedu.test");
        Assert.NotNull(assigned);
        Assert.True(await assertionUsers.IsInRoleAsync(assigned, GlobalRoles.PlatformAdministrator));
        var db = assertionScope.ServiceProvider.GetRequiredService<GiddyEduDbContext>();
        Assert.Empty(await db.TenantMemberships.IgnoreQueryFilters().Where(x => x.UserId == assigned.Id).ToListAsync());
        Assert.Single(await db.PlatformAuditRecords.Where(x => x.ActorUserId == assigned.Id).ToListAsync());
    }
    private static DefaultHttpContext AuthenticatedContext(params Claim[] additionalClaims)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }.Concat(additionalClaims);
        return new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
    }
}
