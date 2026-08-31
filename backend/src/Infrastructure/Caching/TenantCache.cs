using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using StackExchange.Redis;

namespace GiddyEdu.Infrastructure.Caching;

public interface ITenantCache
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan expiry);
    Task RemoveAsync(string key);
}

public sealed class TenantCache(IConnectionMultiplexer redis, ITenantContext tenantContext) : ITenantCache
{
    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await redis.GetDatabase().StringGetAsync(Scoped(key));
        return value.HasValue ? JsonSerializer.Deserialize<T>((string)value!) : default;
    }
    public Task SetAsync<T>(string key, T value, TimeSpan expiry) => redis.GetDatabase().StringSetAsync(Scoped(key), JsonSerializer.Serialize(value), expiry);
    public Task RemoveAsync(string key) => redis.GetDatabase().KeyDeleteAsync(Scoped(key));
    private string Scoped(string key) => $"tenant:{tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.")}:{key}";
}
