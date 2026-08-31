using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Platform;

public interface ISettingsService
{
    Task<T?> GetAsync<T>(string key, Guid? campusId = null, CancellationToken cancellationToken = default);
}

public sealed class SettingsService(GiddyEduDbContext db, ITenantContext tenant) : ISettingsService
{
    public async Task<T?> GetAsync<T>(string key, Guid? campusId = null, CancellationToken cancellationToken = default)
    {
        _ = tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        string? json = null;
        if (campusId.HasValue)
            json = await db.CampusSettings.Where(x => x.CampusId == campusId && x.Key == key).Select(x => x.ValueJson).SingleOrDefaultAsync(cancellationToken);
        json ??= await db.TenantSettings.Where(x => x.Key == key).Select(x => x.ValueJson).SingleOrDefaultAsync(cancellationToken);
        return json is null ? default : JsonSerializer.Deserialize<T>(json);
    }
}

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class FeatureFlagService(GiddyEduDbContext db, Microsoft.Extensions.Hosting.IHostEnvironment environment) : IFeatureFlagService
{
    public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default) =>
        await db.FeatureFlags.Where(x => x.Key == key && x.Environment == environment.EnvironmentName).Select(x => x.Enabled).SingleOrDefaultAsync(cancellationToken);
}
