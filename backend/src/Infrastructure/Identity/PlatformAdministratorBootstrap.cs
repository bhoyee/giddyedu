using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Modules.Platform.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

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
        var displayName = configuration["PlatformBootstrap:DisplayName"]?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("Enabled platform bootstrap requires email and display name configuration.");

        await using var scope = scopes.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var db = scope.ServiceProvider.GetRequiredService<GiddyEduDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
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
        {
            user = new PlatformUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, DisplayName = displayName, CreatedAtUtc = clock.UtcNow };
            var inaccessibleInitialPassword = $"{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}aA1!";
            EnsureSucceeded(await users.CreateAsync(user, inaccessibleInitialPassword), "Platform administrator account creation");
        }
        else if (!user.IsActive || !user.EmailConfirmed)
        {
            throw new InvalidOperationException("The configured platform administrator account must be active and email-confirmed.");
        }
        EnsureSucceeded(await users.AddToRoleAsync(user, GlobalRoles.PlatformAdministrator), "Platform administrator role assignment");
        db.PlatformAuditRecords.Add(new PlatformAuditRecord(Guid.NewGuid(), user.Id, "PlatformAdministrator.Bootstrap", "PlatformUser", user.Id.ToString(), "Succeeded", clock.UtcNow, null));
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Security audit: global platform administrator role assigned to user {UserId}. Disable PlatformBootstrap configuration and use password reset to establish the operator credential.", user.Id);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded) throw new InvalidOperationException($"{operation} failed: {string.Join("; ", result.Errors.Select(x => x.Code))}");
    }
}
