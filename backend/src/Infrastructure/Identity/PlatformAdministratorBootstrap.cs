using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GiddyEdu.Infrastructure.Identity;

public sealed class PlatformAdministratorBootstrap(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<PlatformAdministratorBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("PlatformBootstrap:Enabled")) return;
        var email = configuration["PlatformBootstrap:Email"]?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Enabled platform bootstrap requires an email address.");

        await using var scope = scopes.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var db = scope.ServiceProvider.GetRequiredService<GiddyEduDbContext>();
        if (!await roles.RoleExistsAsync(GlobalRoles.PlatformAdministrator))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole<Guid>(GlobalRoles.PlatformAdministrator));
            EnsureSucceeded(roleResult, "Platform administrator role creation");
        }
        if ((await users.GetUsersInRoleAsync(GlobalRoles.PlatformAdministrator)).Count > 0)
        {
            logger.LogInformation("Platform administrator bootstrap skipped because the global role is already assigned.");
            return;
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
            throw new InvalidOperationException("The configured platform administrator must be an existing GiddyEdu account. Register or invite the account before enabling bootstrap.");
        if (!user.IsActive || !user.EmailConfirmed)
        {
            throw new InvalidOperationException("The configured platform administrator account must be active and email-confirmed.");
        }
        if (!await db.TenantMemberships.IgnoreQueryFilters().AnyAsync(x => x.UserId == user.Id && x.IsActive, ct))
            throw new InvalidOperationException("The configured platform administrator needs an active tenant membership so the existing tenant-scoped sign-in flow can issue a session.");
        EnsureSucceeded(await users.AddToRoleAsync(user, GlobalRoles.PlatformAdministrator), "Platform administrator role assignment");
        logger.LogWarning("Security audit: global platform administrator role assigned to user {UserId}. Disable PlatformBootstrap configuration now.", user.Id);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded) throw new InvalidOperationException($"{operation} failed: {string.Join("; ", result.Errors.Select(x => x.Code))}");
    }
}
