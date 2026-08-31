using System.Text;
using Amazon.S3.Model;
using GiddyEdu.Infrastructure.Storage;

namespace GiddyEdu.IntegrationTests;

public sealed class R2FoundationTests
{
    [Fact]
    public async Task ConfiguredR2_CanWriteReadAndDeleteTenantObject()
    {
        var options = new R2Options
        {
            Endpoint = Environment.GetEnvironmentVariable("R2__ENDPOINT") ?? string.Empty,
            Region = Environment.GetEnvironmentVariable("R2__REGION") ?? "auto",
            Bucket = Environment.GetEnvironmentVariable("R2__BUCKET") ?? string.Empty,
            AccessKey = Environment.GetEnvironmentVariable("R2__ACCESSKEY") ?? string.Empty,
            SecretKey = Environment.GetEnvironmentVariable("R2__SECRETKEY") ?? string.Empty
        };
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_LIVE_R2_TESTS"), "true", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.Bucket) || string.IsNullOrWhiteSpace(options.AccessKey) || string.IsNullOrWhiteSpace(options.SecretKey)) return;

        var originalNoProxy = Environment.GetEnvironmentVariable("NO_PROXY");
        var host = new Uri(options.Endpoint).Host;
        Environment.SetEnvironmentVariable("NO_PROXY", string.IsNullOrWhiteSpace(originalNoProxy) ? host : $"{originalNoProxy},{host}");
        using var client = R2ClientFactory.Create(options);
        var key = new TenantObjectKeyFactory().Create(Guid.NewGuid(), "verification", "phase-zero", Guid.NewGuid(), Guid.NewGuid(), "txt");
        try
        {
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("GiddyEdu Phase 0 storage verification"));
            await client.PutObjectAsync(new PutObjectRequest { BucketName = options.Bucket, Key = key, InputStream = content, ContentType = "text/plain", DisablePayloadSigning = true, UseChunkEncoding = false });
            var metadata = await client.GetObjectMetadataAsync(options.Bucket, key);
            Assert.True(metadata.ContentLength > 0);
        }
        finally
        {
            try { await client.DeleteObjectAsync(options.Bucket, key); }
            finally { Environment.SetEnvironmentVariable("NO_PROXY", originalNoProxy); }
        }
    }
}
