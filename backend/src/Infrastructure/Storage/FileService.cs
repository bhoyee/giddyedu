using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Storage;

public sealed record FileUpload(Guid FileId, string UploadUrl);

public interface IFileService
{
    Task<FileUpload> BeginUploadAsync(string fileName, string contentType, long sizeBytes, string category, string entityType, Guid entityId, Guid userId, CancellationToken cancellationToken = default);
    Task CompleteUploadAsync(Guid fileId, string checksum, CancellationToken cancellationToken = default);
    Task UploadContentAsync(Guid fileId, Stream content, string contentType, long? contentLength, CancellationToken cancellationToken = default);
    Task<string> CreateDownloadUrlAsync(Guid fileId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default);
}

public sealed class FileService(GiddyEduDbContext db, ITenantContext tenant, IObjectKeyFactory keys, IFileObjectStorage storage, IClock clock) : IFileService
{
    private const long MaximumBytes = 50 * 1024 * 1024;
    public async Task<FileUpload> BeginUploadAsync(string fileName, string contentType, long sizeBytes, string category, string entityType, Guid entityId, Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        if (sizeBytes is <= 0 or > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (string.IsNullOrWhiteSpace(contentType) || !contentType.Contains('/')) throw new ArgumentException("A valid content type is required.", nameof(contentType));
        var extension = Path.GetExtension(fileName);
        var id = Guid.NewGuid();
        var objectKey = keys.Create(tenantId, category, entityType, entityId, id, extension);
        db.StoredFiles.Add(new StoredFile(id, tenantId, objectKey, Path.GetFileName(fileName), contentType, sizeBytes, category, entityType, entityId, userId, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        return new(id, storage.CreateUploadUrl(objectKey, contentType));
    }
    public async Task CompleteUploadAsync(Guid fileId, string checksum, CancellationToken cancellationToken = default)
    {
        var file = await db.StoredFiles.SingleOrDefaultAsync(x => x.Id == fileId, cancellationToken) ?? throw new KeyNotFoundException("File not found.");
        if (!await storage.ExistsAsync(file.ObjectKey, cancellationToken)) throw new InvalidOperationException("The uploaded object does not exist.");
        file.MarkAvailable(checksum); await db.SaveChangesAsync(cancellationToken);
    }
    public async Task UploadContentAsync(Guid fileId, Stream content, string contentType, long? contentLength, CancellationToken cancellationToken = default)
    {
        var file = await db.StoredFiles.SingleOrDefaultAsync(x => x.Id == fileId, cancellationToken) ?? throw new KeyNotFoundException("File not found.");
        if (!string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("The uploaded content type does not match the file reservation.", nameof(contentType));
        if (contentLength.HasValue && contentLength.Value != file.SizeBytes) throw new ArgumentException("The uploaded content length does not match the file reservation.", nameof(contentLength));
        await using var buffered = new MemoryStream(file.SizeBytes <= int.MaxValue ? (int)file.SizeBytes : 0);
        var chunk = new byte[81920];
        while (buffered.Length <= file.SizeBytes)
        {
            var read = await content.ReadAsync(chunk.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (buffered.Length + read > file.SizeBytes) throw new ArgumentException("The uploaded content exceeds the file reservation.", nameof(content));
            await buffered.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        if (buffered.Length != file.SizeBytes) throw new ArgumentException("The uploaded content length does not match the file reservation.", nameof(content));
        buffered.Position = 0;
        await storage.UploadAsync(file.ObjectKey, file.ContentType, buffered, cancellationToken);
    }
    public async Task<string> CreateDownloadUrlAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.Status == StoredFileStatus.Available, cancellationToken) ?? throw new KeyNotFoundException("File not found.");
        return storage.CreateDownloadUrl(file.ObjectKey);
    }
    public async Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await db.StoredFiles.SingleOrDefaultAsync(x => x.Id == fileId, cancellationToken) ?? throw new KeyNotFoundException("File not found.");
        await storage.DeleteAsync(file.ObjectKey, cancellationToken); file.MarkDeleted(); await db.SaveChangesAsync(cancellationToken);
    }
}
