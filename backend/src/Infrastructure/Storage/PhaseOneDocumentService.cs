using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Storage;

public sealed record BeginDocumentUploadInput(string FileName, string ContentType, long SizeBytes, string Category);
public sealed record DocumentInfo(Guid Id, string FileName, string ContentType, long SizeBytes, string Category, StoredFileStatus Status, DateTimeOffset CreatedAtUtc);

public interface IPhaseOneDocumentService
{
    Task<IReadOnlyList<DocumentInfo>> ListAsync(Guid actor, string entityType, Guid entityId, CancellationToken ct = default);
    Task<FileUpload> BeginUploadAsync(Guid actor, string entityType, Guid entityId, BeginDocumentUploadInput input, CancellationToken ct = default);
    Task CompleteUploadAsync(Guid actor, Guid fileId, string checksum, CancellationToken ct = default);
    Task<string> CreateDownloadUrlAsync(Guid actor, Guid fileId, CancellationToken ct = default);
    Task DeleteAsync(Guid actor, Guid fileId, CancellationToken ct = default);
}

public sealed class PhaseOneDocumentService(GiddyEduDbContext db, IFeatureAccessGuard access, IFileService files) : IPhaseOneDocumentService
{
    private const long MaximumDocumentBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) { "application/pdf", "image/jpeg", "image/png" };
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase) { "photo", "identity", "admission", "medical", "qualification", "contract", "other" };

    public async Task<IReadOnlyList<DocumentInfo>> ListAsync(Guid actor, string entityType, Guid entityId, CancellationToken ct = default)
    {
        var target = await AuthorizeTargetAsync(actor, entityType, entityId, false, ct);
        return await db.StoredFiles.AsNoTracking().Where(x => x.EntityType == target && x.EntityId == entityId && x.Status != StoredFileStatus.Deleted)
            .OrderByDescending(x => x.CreatedAtUtc).Select(x => new DocumentInfo(x.Id, x.OriginalFileName, x.ContentType, x.SizeBytes, x.Category, x.Status, x.CreatedAtUtc)).ToListAsync(ct);
    }

    public async Task<FileUpload> BeginUploadAsync(Guid actor, string entityType, Guid entityId, BeginDocumentUploadInput input, CancellationToken ct = default)
    {
        var target = await AuthorizeTargetAsync(actor, entityType, entityId, true, ct);
        if (input.SizeBytes is <= 0 or > MaximumDocumentBytes) throw new ArgumentOutOfRangeException(nameof(input), "Documents must not exceed 10 MB.");
        if (!AllowedContentTypes.Contains(input.ContentType)) throw new ArgumentException("Only PDF, JPEG, and PNG documents are supported.", nameof(input));
        var category = input.Category.Trim().ToLowerInvariant(); if (!AllowedCategories.Contains(category)) throw new ArgumentException("Document category is not supported.", nameof(input));
        return await files.BeginUploadAsync(input.FileName, input.ContentType, input.SizeBytes, category, target, entityId, actor, ct);
    }

    public async Task CompleteUploadAsync(Guid actor, Guid fileId, string checksum, CancellationToken ct = default)
    { var file = await FindAndAuthorizeAsync(actor, fileId, true, ct); if (file.Status != StoredFileStatus.PendingUpload) throw new InvalidOperationException("Only pending documents can be completed."); await files.CompleteUploadAsync(fileId, checksum, ct); }
    public async Task<string> CreateDownloadUrlAsync(Guid actor, Guid fileId, CancellationToken ct = default)
    { await FindAndAuthorizeAsync(actor, fileId, false, ct); return await files.CreateDownloadUrlAsync(fileId, ct); }
    public async Task DeleteAsync(Guid actor, Guid fileId, CancellationToken ct = default)
    { await FindAndAuthorizeAsync(actor, fileId, true, ct); await files.DeleteAsync(fileId, ct); }

    private async Task<StoredFile> FindAndAuthorizeAsync(Guid actor, Guid fileId, bool manage, CancellationToken ct)
    { var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.Status != StoredFileStatus.Deleted, ct) ?? throw new KeyNotFoundException("Document was not found."); await AuthorizeTargetAsync(actor, file.EntityType, file.EntityId, manage, ct); return file; }

    private async Task<string> AuthorizeTargetAsync(Guid actor, string entityType, Guid entityId, bool manage, CancellationToken ct)
    {
        var target = entityType.Trim().ToLowerInvariant() switch { "applicant" => "Applicant", "student" => "Student", "staff" or "staffprofile" => "StaffProfile", _ => throw new ArgumentException("Document target is not supported.", nameof(entityType)) };
        var (permission, feature) = target switch
        {
            "Applicant" => (manage ? Permissions.AdmissionsSensitiveManage : Permissions.AdmissionsSensitiveView, FeatureKeys.Admissions),
            "Student" => (manage ? Permissions.StudentsSensitiveManage : Permissions.StudentsSensitiveView, FeatureKeys.StudentInformation),
            _ => (manage ? Permissions.StaffManage : Permissions.StaffSensitiveView, FeatureKeys.StaffManagement)
        };
        await access.DemandAsync(actor, permission, feature, ct);
        var exists = target switch { "Applicant" => await db.Applicants.AnyAsync(x => x.Id == entityId, ct), "Student" => await db.Students.AnyAsync(x => x.Id == entityId, ct), _ => await db.StaffProfiles.AnyAsync(x => x.Id == entityId, ct) };
        if (!exists) throw new KeyNotFoundException("Document target was not found."); return target;
    }
}
