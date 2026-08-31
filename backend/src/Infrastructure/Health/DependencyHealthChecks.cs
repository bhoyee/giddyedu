using Amazon.S3;
using GiddyEdu.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GiddyEdu.Infrastructure.Health;

public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try { await redis.GetDatabase().PingAsync(); return HealthCheckResult.Healthy(); }
        catch (Exception exception) { return HealthCheckResult.Unhealthy("Redis is unavailable.", exception); }
    }
}

public sealed class R2HealthCheck(IAmazonS3 client, IOptions<R2Options> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request
            {
                BucketName = options.Value.Bucket,
                MaxKeys = 1
            }, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) { return HealthCheckResult.Unhealthy("Cloudflare R2 is unavailable.", exception); }
    }
}
