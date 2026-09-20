using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace GiddyEdu.Infrastructure.Storage;

public sealed class R2Options
{
    public const string SectionName = "R2";
    public string Endpoint { get; init; } = string.Empty;
    public string Region { get; init; } = "auto";
    public string Bucket { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public int PresignedUrlMinutes { get; init; } = 10;
}

public interface IObjectKeyFactory
{
    string Create(Guid tenantId, string category, string entityType, Guid entityId, Guid fileId, string extension);
}

public sealed class TenantObjectKeyFactory : IObjectKeyFactory
{
    public string Create(Guid tenantId, string category, string entityType, Guid entityId, Guid fileId, string extension)
    {
        if (tenantId == Guid.Empty || entityId == Guid.Empty || fileId == Guid.Empty) throw new ArgumentException("Object-key identifiers are required.");
        var safeCategory = Normalize(category);
        var safeEntity = Normalize(entityType);
        var safeExtension = extension.Trim().TrimStart('.').ToLowerInvariant();
        if (safeExtension.Length is 0 or > 10 || safeExtension.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid file extension.", nameof(extension));
        return $"tenants/{tenantId:D}/{safeCategory}/{safeEntity}/{entityId:D}/{fileId:D}.{safeExtension}";
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length is 0 or > 80 || normalized.Any(c => !char.IsLetterOrDigit(c) && c != '-')) throw new ArgumentException("Object-key segment is invalid.");
        return normalized;
    }
}

public interface IFileObjectStorage
{
    string CreateUploadUrl(string objectKey, string contentType);
    string CreateDownloadUrl(string objectKey);
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
    Task UploadAsync(string objectKey, string contentType, Stream content, CancellationToken cancellationToken);
    Task<string> ReadTextAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken);
    Task<byte[]> ReadBytesAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken);
    Task WriteTextAsync(string objectKey, string contentType, string value, CancellationToken cancellationToken);
}

public sealed class R2FileObjectStorage(IAmazonS3 client, IOptions<R2Options> options) : IFileObjectStorage
{
    private readonly R2Options settings = options.Value;
    public string CreateUploadUrl(string objectKey, string contentType) => client.GetPreSignedURL(new GetPreSignedUrlRequest { BucketName = settings.Bucket, Key = objectKey, Verb = HttpVerb.PUT, ContentType = contentType, Expires = DateTime.UtcNow.AddMinutes(settings.PresignedUrlMinutes) });
    public string CreateDownloadUrl(string objectKey) => client.GetPreSignedURL(new GetPreSignedUrlRequest { BucketName = settings.Bucket, Key = objectKey, Verb = HttpVerb.GET, Expires = DateTime.UtcNow.AddMinutes(settings.PresignedUrlMinutes) });
    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken)
    {
        try { await client.GetObjectMetadataAsync(settings.Bucket, objectKey, cancellationToken); return true; }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound) { return false; }
    }
    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => client.DeleteObjectAsync(settings.Bucket, objectKey, cancellationToken);
    public async Task UploadAsync(string objectKey, string contentType, Stream content, CancellationToken cancellationToken)
    {
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = settings.Bucket,
            Key = objectKey,
            ContentType = contentType,
            InputStream = content,
            UseChunkEncoding = false
        }, cancellationToken);
    }
    public async Task<string> ReadTextAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken)
        => System.Text.Encoding.UTF8.GetString(await ReadBytesAsync(objectKey, maximumBytes, cancellationToken));

    public async Task<byte[]> ReadBytesAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken)
    {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        using var response = await client.GetObjectAsync(settings.Bucket, objectKey, cancellationToken);
        if (response.ContentLength > maximumBytes) throw new InvalidOperationException("The stored file exceeds the permitted import size.");
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await response.ResponseStream.ReadAsync(chunk.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (buffer.Length + read > maximumBytes) throw new InvalidOperationException("The stored file exceeds the permitted import size.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        return buffer.ToArray();
    }
    public async Task WriteTextAsync(string objectKey, string contentType, string value, CancellationToken cancellationToken)
    {
        using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(value));
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = settings.Bucket,
            Key = objectKey,
            ContentType = contentType,
            InputStream = content,
            UseChunkEncoding = false
        }, cancellationToken);
    }
}

public static class R2ClientFactory
{
    public static IAmazonS3 Create(R2Options options)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = options.Region,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };
        return new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
    }
}
