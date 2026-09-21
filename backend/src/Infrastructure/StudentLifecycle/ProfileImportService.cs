using System.Data;
using System.Globalization;
using System.Net.Mail;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Identity;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.Modules.Subscriptions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

public interface IProfileImportService
{
    Task<ImportUploadInfo> BeginStudentsAsync(Guid actor, ImportUploadInput input, CancellationToken ct = default);
    Task<byte[]> StudentTemplateAsync(Guid actor, bool excel, CancellationToken ct = default);
    Task UploadStudentContentAsync(Guid actor, Guid operationId, Stream content, string contentType, long? contentLength, CancellationToken ct = default);
    Task<ImportUploadInfo> BeginGuardiansAsync(Guid actor, ImportUploadInput input, CancellationToken ct = default);
    Task UploadGuardianContentAsync(Guid actor, Guid operationId, Stream content, string contentType, long? contentLength, CancellationToken ct = default);
    Task<GuardianImportPreview> PreviewGuardiansAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default);
    Task ConfirmGuardiansAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<byte[]> GuardianTemplateAsync(Guid actor, bool excel, CancellationToken ct = default);
    Task DeleteGuardianDraftAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task QueueStudentsAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default);
    Task QueueGuardiansAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default);
    Task<ImportOperationInfo> GetStudentsAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<ImportOperationInfo> GetGuardiansAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<IReadOnlyList<ImportOperationInfo>> ListStudentsAsync(Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<ImportOperationInfo>> ListGuardiansAsync(Guid actor, CancellationToken ct = default);
    Task<string> GetStudentErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<string> GetGuardianErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default);
}

public sealed record GuardianImportPreview(Guid OperationId, int TotalRows, int ValidRows, int RejectedRows,
    IReadOnlyList<GuardianImportPreviewRow> Rows);
public sealed record GuardianImportPreviewRow(int Row, string AdmissionNumber, string GuardianName, string? StudentName,
    string? ClassName, string Relationship, string? Error);

public sealed class ProfileImportService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IFileService files,
    IClock clock, IBackgroundJobClient jobs, IFileObjectStorage storage, IPermissionService permissions) : IProfileImportService
{
    private const long MaximumImportBytes = 5 * 1024 * 1024;
    public Task<ImportUploadInfo> BeginStudentsAsync(Guid actor, ImportUploadInput input, CancellationToken ct = default) => BeginAsync(actor, input, "Students", "StudentImport", Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
    public async Task<byte[]> StudentTemplateAsync(Guid actor, bool excel, CancellationToken ct = default)
    { await access.DemandAsync(actor, Permissions.StudentsManage, FeatureKeys.StudentInformation, ct); return StudentImportFile.Template(excel); }
    public async Task UploadStudentContentAsync(Guid actor, Guid operationId, Stream content, string contentType, long? contentLength, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
        var operation = await db.ImportOperations.SingleOrDefaultAsync(x => x.Id == operationId && x.ImportType == "Students", ct) ?? throw new KeyNotFoundException("Student import was not found.");
        if (operation.Status != ImportOperationStatus.AwaitingUpload) throw new InvalidOperationException("This student import has already been submitted.");
        var file = await db.StoredFiles.SingleOrDefaultAsync(x => x.EntityType == "StudentImport" && x.EntityId == operationId, ct) ?? throw new KeyNotFoundException("Student import file was not found.");
        if (file.Status != StoredFileStatus.PendingUpload) throw new InvalidOperationException("Only a pending student import can receive a file.");
        await files.UploadContentAsync(file.Id, content, contentType, contentLength, ct);
    }
    public async Task<ImportUploadInfo> BeginGuardiansAsync(Guid actor, ImportUploadInput input, CancellationToken ct = default)
    {
        await DemandGuardianImportAsync(actor, ct);
        var campusId = tenant.CampusId ?? throw new InvalidOperationException("Select an active campus before importing guardians.");
        var extension = Path.GetExtension(input.FileName).ToLowerInvariant();
        if (extension is not (".csv" or ".xlsx") || input.SizeBytes is <= 0 or > MaximumImportBytes)
            throw new ArgumentException("Choose a CSV or Excel (.xlsx) file no larger than 5 MB.");
        if (extension == ".csv" && input.ContentType is not ("text/csv" or "text/plain" or "application/csv" or "application/vnd.ms-excel" or "application/octet-stream")
            || extension == ".xlsx" && input.ContentType is not ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or "application/zip" or "application/octet-stream"))
            throw new ArgumentException("The file type does not match its extension.");
        var operation = new ImportOperation(Guid.NewGuid(), tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required."),
            "Guardians", actor, clock.UtcNow, campusId);
        db.ImportOperations.Add(operation);
        await db.SaveChangesAsync(ct);
        var upload = await files.BeginUploadAsync(input.FileName, input.ContentType, input.SizeBytes, "imports", "GuardianImport", operation.Id, actor, ct);
        return new(operation.Id, upload.FileId, upload.UploadUrl);
    }
    public Task QueueStudentsAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default) => QueueAsync(actor, operationId, checksum, "Students", "StudentImport", Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
    public async Task QueueGuardiansAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default)
    { await PreviewGuardiansAsync(actor, operationId, checksum, ct); await ConfirmGuardiansAsync(actor, operationId, ct); }
    public Task<ImportOperationInfo> GetStudentsAsync(Guid actor, Guid operationId, CancellationToken ct = default) => GetAsync(actor, operationId, "Students", Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
    public Task<ImportOperationInfo> GetGuardiansAsync(Guid actor, Guid operationId, CancellationToken ct = default) => GetAsync(actor, operationId, "Guardians", Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);
    public Task<IReadOnlyList<ImportOperationInfo>> ListStudentsAsync(Guid actor, CancellationToken ct = default) => ListAsync(actor, "Students", Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
    public Task<IReadOnlyList<ImportOperationInfo>> ListGuardiansAsync(Guid actor, CancellationToken ct = default) => ListAsync(actor, "Guardians", Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);
    public Task<string> GetStudentErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default) => GetErrorDownloadAsync(actor, operationId, "Students", Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
    public Task<string> GetGuardianErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default) => GetErrorDownloadAsync(actor, operationId, "Guardians", Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);

    public async Task UploadGuardianContentAsync(Guid actor, Guid operationId, Stream content, string contentType, long? contentLength, CancellationToken ct = default)
    {
        await DemandGuardianImportAsync(actor, ct);
        var operation = await FindGuardianOperationAsync(operationId, ct);
        if (operation.Status != ImportOperationStatus.AwaitingUpload) throw new InvalidOperationException("This guardian import has already been submitted.");
        var file = await FindGuardianFileAsync(operationId, ct);
        if (file.Status != StoredFileStatus.PendingUpload) throw new InvalidOperationException("Only a pending guardian import can receive a file.");
        await files.UploadContentAsync(file.Id, content, contentType, contentLength, ct);
    }

    public async Task<GuardianImportPreview> PreviewGuardiansAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default)
    {
        await DemandGuardianImportAsync(actor, ct);
        var operation = await FindGuardianOperationAsync(operationId, ct);
        if (operation.Status != ImportOperationStatus.AwaitingUpload) throw new InvalidOperationException("This import has already been submitted.");
        var file = await FindGuardianFileAsync(operationId, ct);
        if (file.Status == StoredFileStatus.PendingUpload) await files.CompleteUploadAsync(file.Id, checksum, ct);
        else if (file.Status != StoredFileStatus.Available || !string.Equals(file.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The upload checksum does not match the reviewed file.");
        var rows = GuardianImportFile.Read(await storage.ReadBytesAsync(file.ObjectKey, MaximumImportBytes, ct), file.OriginalFileName);
        var reviewed = await GuardianImportValidator.ValidateAsync(db, rows, RequireCampus(), ct);
        return new(operation.Id, reviewed.Count, reviewed.Count(row => row.Error is null), reviewed.Count(row => row.Error is not null),
            reviewed.Select(row => new GuardianImportPreviewRow(row.Row, row.AdmissionNumber, $"{row.FirstName} {row.LastName}",
                row.StudentName, row.ClassName, row.Relationship.ToString(), row.Error)).ToArray());
    }

    public async Task ConfirmGuardiansAsync(Guid actor, Guid operationId, CancellationToken ct = default)
    {
        await DemandGuardianImportAsync(actor, ct);
        var operation = await FindGuardianOperationAsync(operationId, ct);
        if (operation.Status != ImportOperationStatus.AwaitingUpload) throw new InvalidOperationException("This import has already been submitted.");
        var file = await FindGuardianFileAsync(operationId, ct);
        if (file.Status != StoredFileStatus.Available) throw new InvalidOperationException("Preview the file before importing guardians.");
        var rows = GuardianImportFile.Read(await storage.ReadBytesAsync(file.ObjectKey, MaximumImportBytes, ct), file.OriginalFileName);
        var reviewed = await GuardianImportValidator.ValidateAsync(db, rows, RequireCampus(), ct);
        if (reviewed.All(row => row.Error is not null))
            throw new InvalidOperationException("No valid guardian relationships are available. Correct the file and upload it again.");
        operation.Queue(file.Id);
        await db.SaveChangesAsync(ct);
        jobs.Enqueue<ProfileImportJob>(job => job.ProcessAsync(operation.TenantId, operation.Id, CancellationToken.None));
    }

    public async Task<byte[]> GuardianTemplateAsync(Guid actor, bool excel, CancellationToken ct = default)
    { await DemandGuardianImportAsync(actor, ct); RequireCampus(); return GuardianImportFile.Template(excel); }

    public async Task DeleteGuardianDraftAsync(Guid actor, Guid operationId, CancellationToken ct = default)
    {
        await DemandGuardianImportAsync(actor, ct);
        var operation = await FindGuardianOperationAsync(operationId, ct);
        if (operation.Status != ImportOperationStatus.AwaitingUpload)
            throw new InvalidOperationException("Only an awaiting-upload guardian import can be deleted.");
        var file = await db.StoredFiles.SingleOrDefaultAsync(x => x.EntityType == "GuardianImport" && x.EntityId == operationId, ct);
        if (file is not null) { await files.DeleteAsync(file.Id, ct); db.StoredFiles.Remove(file); }
        db.ImportOperations.Remove(operation);
        await db.SaveChangesAsync(ct);
    }

    private async Task DemandGuardianImportAsync(Guid actor, CancellationToken ct)
    {
        await access.DemandAsync(actor, Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);
        await access.DemandAsync(actor, Permissions.StudentsView, FeatureKeys.StudentInformation, ct);
        if (!await permissions.HasPermissionAsync(actor, Permissions.UsersManage, ct))
            throw new UnauthorizedAccessException("Users.Manage permission is required to invite imported guardians.");
    }
    private Guid RequireCampus() => tenant.CampusId ?? throw new InvalidOperationException("Select an active campus before importing guardians.");
    private async Task<ImportOperation> FindGuardianOperationAsync(Guid id, CancellationToken ct) =>
        await db.ImportOperations.SingleOrDefaultAsync(x => x.Id == id && x.ImportType == "Guardians" && x.CampusId == RequireCampus(), ct)
            ?? throw new KeyNotFoundException("Guardian import was not found in this campus.");
    private async Task<StoredFile> FindGuardianFileAsync(Guid operationId, CancellationToken ct) =>
        await db.StoredFiles.SingleOrDefaultAsync(x => x.EntityType == "GuardianImport" && x.EntityId == operationId, ct)
            ?? throw new KeyNotFoundException("Guardian import file was not found.");

    private async Task<ImportUploadInfo> BeginAsync(Guid actor, ImportUploadInput input, string importType, string entityType, string permission, string feature, CancellationToken ct)
    {
        await access.DemandAsync(actor, permission, feature, ct); ValidateUpload(input);
        var operation = new ImportOperation(Guid.NewGuid(), tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required."), importType, actor, clock.UtcNow);
        db.ImportOperations.Add(operation); await db.SaveChangesAsync(ct);
        var upload = await files.BeginUploadAsync(input.FileName, input.ContentType, input.SizeBytes, "imports", entityType, operation.Id, actor, ct);
        return new(operation.Id, upload.FileId, upload.UploadUrl);
    }

    private async Task QueueAsync(Guid actor, Guid operationId, string checksum, string importType, string entityType, string permission, string feature, CancellationToken ct)
    {
        await access.DemandAsync(actor, permission, feature, ct);
        var operation = await db.ImportOperations.SingleOrDefaultAsync(x => x.Id == operationId && x.ImportType == importType, ct) ?? throw new KeyNotFoundException("Import operation was not found.");
        var file = await db.StoredFiles.SingleOrDefaultAsync(x => x.EntityType == entityType && x.EntityId == operationId, ct) ?? throw new KeyNotFoundException("Import upload was not found.");
        if (file.Status == StoredFileStatus.PendingUpload) await files.CompleteUploadAsync(file.Id, checksum, ct);
        else if (file.Status != StoredFileStatus.Available || !string.Equals(file.Checksum, checksum, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The upload checksum does not match the submitted file.");
        operation.Queue(file.Id); await db.SaveChangesAsync(ct);
        jobs.Enqueue<ProfileImportJob>(job => job.ProcessAsync(operation.TenantId, operation.Id, CancellationToken.None));
    }

    private async Task<ImportOperationInfo> GetAsync(Guid actor, Guid operationId, string importType, string permission, string feature, CancellationToken ct)
    {
        await access.DemandAsync(actor, permission, feature, ct);
        var query = db.ImportOperations.AsNoTracking().Where(x => x.Id == operationId && x.ImportType == importType);
        if (importType == "Guardians") { var campusId = RequireCampus(); query = query.Where(x => x.CampusId == campusId); }
        return await query.Select(x => new ImportOperationInfo(x.Id, x.ImportType, x.Status, x.TotalRows, x.ImportedRows, x.RejectedRows, x.ErrorSummary, x.ErrorFileId, x.CreatedAtUtc, x.CompletedAtUtc)).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Import operation was not found.");
    }

    private async Task<IReadOnlyList<ImportOperationInfo>> ListAsync(Guid actor, string importType, string permission, string feature, CancellationToken ct)
    {
        await access.DemandAsync(actor, permission, feature, ct);
        var query = db.ImportOperations.AsNoTracking().Where(x => x.ImportType == importType);
        if (importType == "Guardians") { var campusId = RequireCampus(); query = query.Where(x => x.CampusId == campusId); }
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(50).Select(x => new ImportOperationInfo(x.Id, x.ImportType, x.Status, x.TotalRows, x.ImportedRows, x.RejectedRows, x.ErrorSummary, x.ErrorFileId, x.CreatedAtUtc, x.CompletedAtUtc)).ToListAsync(ct);
    }

    private async Task<string> GetErrorDownloadAsync(Guid actor, Guid operationId, string importType, string permission, string feature, CancellationToken ct)
    {
        await access.DemandAsync(actor, permission, feature, ct);
        var query = db.ImportOperations.Where(x => x.Id == operationId && x.ImportType == importType);
        if (importType == "Guardians") { var campusId = RequireCampus(); query = query.Where(x => x.CampusId == campusId); }
        var fileId = await query.Select(x => x.ErrorFileId).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Import error report was not found.");
        return await files.CreateDownloadUrlAsync(fileId, ct);
    }

    private static void ValidateUpload(ImportUploadInput input)
    {
        var extension = Path.GetExtension(input.FileName).ToLowerInvariant();
        if (extension is not (".csv" or ".xlsx")) throw new ArgumentException("Imports must use a CSV or Excel (.xlsx) file.");
        if (input.SizeBytes is <= 0 or > MaximumImportBytes) throw new ArgumentOutOfRangeException(nameof(input.SizeBytes), "Imports are limited to 5 MB.");
        if (extension == ".csv" && input.ContentType is not ("text/csv" or "text/plain" or "application/vnd.ms-excel" or "application/octet-stream") || extension == ".xlsx" && input.ContentType is not ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or "application/zip" or "application/octet-stream")) throw new ArgumentException("The file type does not match its extension.");
    }
}

public sealed class ProfileImportJob(GiddyEduDbContext db, ITenantContextSetter tenant, IFileObjectStorage storage, IImportErrorReportWriter reports,
    IAccountInvitationService invitations, IClock clock, ILogger<ProfileImportJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ProcessAsync(Guid tenantId, Guid operationId, CancellationToken ct = default)
    {
        tenant.Set(tenantId, null);
        try
        {
            var operation = await db.ImportOperations.SingleOrDefaultAsync(x => x.Id == operationId, ct) ?? throw new KeyNotFoundException("Import operation was not found.");
            if (operation.Status == ImportOperationStatus.Completed) return;
            if (operation.Status != ImportOperationStatus.Queued) throw new InvalidOperationException("Import operation is not queued.");
            operation.Start(clock.UtcNow); await db.SaveChangesAsync(ct);
            try
            {
                var file = await db.StoredFiles.AsNoTracking().SingleAsync(x => x.Id == operation.SourceFileId && x.Status == StoredFileStatus.Available, ct);
                if (operation.ImportType == "Guardians")
                {
                    if (!operation.CampusId.HasValue) throw new InvalidOperationException("Guardian import has no campus.");
                    tenant.Set(tenantId, operation.CampusId);
                    await ProcessGuardiansAsync(operation, file, ct);
                    return;
                }
                var rows = StudentImportFile.Read(await storage.ReadBytesAsync(file.ObjectKey, 5 * 1024 * 1024, ct), file.OriginalFileName);
                var errors = operation.ImportType switch
                {
                    "Students" => await ImportStudentsAsync(rows, tenantId, ct),
                    _ => ["The import type is unsupported."]
                };
                if (errors.Count > 0) { DetachAddedProfiles(); var reportId = await reports.WriteAsync(operation, errors, ct); operation.Fail(Math.Max(0, rows.Count - 1), errors.Count, string.Join(" ", errors.Take(20)), clock.UtcNow, reportId); await db.SaveChangesAsync(ct); return; }
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                operation.Complete(Math.Max(0, rows.Count - 1), Math.Max(0, rows.Count - 1), clock.UtcNow); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Profile import {ImportOperationId} failed for tenant {TenantId}.", operationId, tenantId); DetachAddedProfiles();
                operation.Fail(operation.TotalRows, operation.RejectedRows, "The import could not be processed. Verify the file format and try again.", clock.UtcNow); await db.SaveChangesAsync(ct);
            }
        }
        finally { tenant.Clear(); }
    }

    private async Task<IReadOnlyList<string>> ImportStudentsAsync(IReadOnlyList<IReadOnlyList<string>> rows, Guid tenantId, CancellationToken ct)
    {
        if (rows.Count == 0) return ["The CSV is empty."];
        if (!rows[0].SequenceEqual(StudentImportFile.Headers, StringComparer.OrdinalIgnoreCase)) return [$"The header must be: {string.Join(",", StudentImportFile.Headers)}."];
        var existing = new HashSet<string>(await db.Students.Select(x => x.AdmissionNumber).ToListAsync(ct), StringComparer.OrdinalIgnoreCase); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var errors = new List<string>();
        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index]; var number = index + 1;
            if (row.Count != StudentImportFile.Headers.Length || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]) || string.IsNullOrWhiteSpace(row[2])) { errors.Add($"Row {number} is missing required values or has an incorrect number of columns."); continue; }
            if (!DateOnly.TryParseExact(row[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate)) { errors.Add($"Row {number} has an invalid date of birth."); continue; }
            var admissionNumber = row[0].Trim().ToUpperInvariant();
            if (admissionNumber.Length > 50 || row[1].Trim().Length > 100 || row[2].Trim().Length > 100 || row[4].Trim().Length > 320) { errors.Add($"Row {number} contains a value that exceeds its permitted length."); continue; }
            if (!string.IsNullOrWhiteSpace(row[4]) && !MailAddress.TryCreate(row[4], out _)) { errors.Add($"Row {number} has an invalid email address."); continue; }
            if (!string.IsNullOrWhiteSpace(row[5]) && !string.Equals(row[5], "Active", StringComparison.OrdinalIgnoreCase)) { errors.Add($"Row {number} has an unsupported initial status."); continue; }
            if (!seen.Add(admissionNumber) || existing.Contains(admissionNumber)) { errors.Add($"Row {number} has a duplicate admission number."); continue; }
            db.Students.Add(new Student(Guid.NewGuid(), tenantId, admissionNumber, row[1], row[2], birthDate, null, clock.UtcNow, string.IsNullOrWhiteSpace(row[4]) ? null : row[4]));
        }
        return errors;
    }

    private async Task ProcessGuardiansAsync(ImportOperation operation, StoredFile file, CancellationToken ct)
    {
        var rows = GuardianImportFile.Read(await storage.ReadBytesAsync(file.ObjectKey, 5 * 1024 * 1024, ct), file.OriginalFileName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var reviewed = await GuardianImportValidator.ValidateAsync(db, rows, operation.CampusId!.Value, ct);
        var errors = reviewed.Where(row => row.Error is not null).Select(row => $"Row {row.Row}: {row.Error}").ToList();
        var newGuardians = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var inviteIds = new HashSet<Guid>();
        foreach (var row in reviewed.Where(row => row.Error is null))
        {
            var guardianId = row.ExistingGuardianId ?? newGuardians.GetValueOrDefault(row.Phone);
            if (guardianId == Guid.Empty)
            {
                guardianId = Guid.NewGuid();
                var guardian = new Guardian(guardianId, operation.TenantId, row.FirstName, row.LastName, row.Phone, row.Email, clock.UtcNow);
                guardian.Update(row.FirstName, row.LastName, row.Phone, row.Email, row.Address);
                db.Guardians.Add(guardian);
                newGuardians.Add(row.Phone, guardianId);
                db.AuditRecords.Add(new AuditRecord(Guid.NewGuid(), operation.TenantId, operation.RequestedByUserId,
                    "Guardian.ImportCreate", "Guardian", guardianId.ToString(), "Succeeded", clock.UtcNow, null));
            }
            db.StudentGuardians.Add(new StudentGuardian(operation.TenantId, row.StudentId!.Value, guardianId,
                row.Relationship, row.IsPrimary, row.IsEmergencyContact, row.MayCollect));
            db.AuditRecords.Add(new AuditRecord(Guid.NewGuid(), operation.TenantId, operation.RequestedByUserId,
                "Guardian.ImportLinkStudent", "Guardian", guardianId.ToString(), "Succeeded", clock.UtcNow, null));
            inviteIds.Add(guardianId);
        }
        await db.SaveChangesAsync(ct);
        var alreadyConnected = await db.Guardians.Where(x => inviteIds.Contains(x.Id) && x.UserId != null).Select(x => x.Id).ToListAsync(ct);
        foreach (var guardianId in inviteIds.Except(alreadyConnected))
            await invitations.CreateAsync(operation.RequestedByUserId,
                new CreateAccountInvitationInput(InvitationTargetType.Guardian, guardianId), ct);
        Guid? errorFileId = null;
        if (errors.Count > 0) errorFileId = await reports.WriteAsync(operation, errors, ct);
        var imported = reviewed.Count(row => row.Error is null);
        if (imported == 0) operation.Fail(reviewed.Count, errors.Count, "No guardian relationships were imported. Download the error report and correct the file.", clock.UtcNow, errorFileId);
        else operation.Complete(reviewed.Count, imported, clock.UtcNow, errors.Count, errorFileId);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void DetachAddedProfiles()
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added && (x.Entity is Student || x.Entity is Guardian))) entry.State = EntityState.Detached;
    }
}
