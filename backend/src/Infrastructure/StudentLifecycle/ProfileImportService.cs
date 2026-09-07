using System.Globalization;
using System.Net.Mail;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.Identity;
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
    Task<ImportUploadInfo> BeginGuardiansAsync(Guid actor, ImportUploadInput input, CancellationToken ct = default);
    Task QueueStudentsAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default);
    Task QueueGuardiansAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default);
    Task<ImportOperationInfo> GetStudentsAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<ImportOperationInfo> GetGuardiansAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<IReadOnlyList<ImportOperationInfo>> ListStudentsAsync(Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<ImportOperationInfo>> ListGuardiansAsync(Guid actor, CancellationToken ct = default);
    Task<string> GetStudentErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<string> GetGuardianErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default);
}

public sealed class ProfileImportService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IFileService files, IClock clock, IBackgroundJobClient jobs) : IProfileImportService
{
    private const long MaximumImportBytes = 5 * 1024 * 1024;
    public Task<ImportUploadInfo> BeginStudentsAsync(Guid actor, ImportUploadInput input, CancellationToken ct = default) => BeginAsync(actor, input, "Students", "StudentImport", Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
    public Task<ImportUploadInfo> BeginGuardiansAsync(Guid actor, ImportUploadInput input, CancellationToken ct = default) => BeginAsync(actor, input, "Guardians", "GuardianImport", Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);
    public Task QueueStudentsAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default) => QueueAsync(actor, operationId, checksum, "Students", "StudentImport", Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
    public Task QueueGuardiansAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default) => QueueAsync(actor, operationId, checksum, "Guardians", "GuardianImport", Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);
    public Task<ImportOperationInfo> GetStudentsAsync(Guid actor, Guid operationId, CancellationToken ct = default) => GetAsync(actor, operationId, "Students", Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
    public Task<ImportOperationInfo> GetGuardiansAsync(Guid actor, Guid operationId, CancellationToken ct = default) => GetAsync(actor, operationId, "Guardians", Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);
    public Task<IReadOnlyList<ImportOperationInfo>> ListStudentsAsync(Guid actor, CancellationToken ct = default) => ListAsync(actor, "Students", Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
    public Task<IReadOnlyList<ImportOperationInfo>> ListGuardiansAsync(Guid actor, CancellationToken ct = default) => ListAsync(actor, "Guardians", Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);
    public Task<string> GetStudentErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default) => GetErrorDownloadAsync(actor, operationId, "Students", Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
    public Task<string> GetGuardianErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default) => GetErrorDownloadAsync(actor, operationId, "Guardians", Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);

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
        var file = await db.StoredFiles.SingleOrDefaultAsync(x => x.EntityType == entityType && x.EntityId == operationId && x.Status == StoredFileStatus.PendingUpload, ct) ?? throw new KeyNotFoundException("Import upload was not found.");
        await files.CompleteUploadAsync(file.Id, checksum, ct); operation.Queue(file.Id); await db.SaveChangesAsync(ct);
        jobs.Enqueue<ProfileImportJob>(job => job.ProcessAsync(operation.TenantId, operation.Id, CancellationToken.None));
    }

    private async Task<ImportOperationInfo> GetAsync(Guid actor, Guid operationId, string importType, string permission, string feature, CancellationToken ct)
    {
        await access.DemandAsync(actor, permission, feature, ct);
        return await db.ImportOperations.AsNoTracking().Where(x => x.Id == operationId && x.ImportType == importType).Select(x => new ImportOperationInfo(x.Id, x.ImportType, x.Status, x.TotalRows, x.ImportedRows, x.RejectedRows, x.ErrorSummary, x.ErrorFileId, x.CreatedAtUtc, x.CompletedAtUtc)).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Import operation was not found.");
    }

    private async Task<IReadOnlyList<ImportOperationInfo>> ListAsync(Guid actor, string importType, string permission, string feature, CancellationToken ct)
    {
        await access.DemandAsync(actor, permission, feature, ct);
        return await db.ImportOperations.AsNoTracking().Where(x => x.ImportType == importType).OrderByDescending(x => x.CreatedAtUtc).Take(50).Select(x => new ImportOperationInfo(x.Id, x.ImportType, x.Status, x.TotalRows, x.ImportedRows, x.RejectedRows, x.ErrorSummary, x.ErrorFileId, x.CreatedAtUtc, x.CompletedAtUtc)).ToListAsync(ct);
    }

    private async Task<string> GetErrorDownloadAsync(Guid actor, Guid operationId, string importType, string permission, string feature, CancellationToken ct)
    {
        await access.DemandAsync(actor, permission, feature, ct);
        var fileId = await db.ImportOperations.Where(x => x.Id == operationId && x.ImportType == importType).Select(x => x.ErrorFileId).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Import error report was not found.");
        return await files.CreateDownloadUrlAsync(fileId, ct);
    }

    private static void ValidateUpload(ImportUploadInput input)
    {
        if (!string.Equals(Path.GetExtension(input.FileName), ".csv", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Imports must use a .csv file.");
        if (input.SizeBytes is <= 0 or > MaximumImportBytes) throw new ArgumentOutOfRangeException(nameof(input.SizeBytes), "Imports are limited to 5 MB.");
        if (!string.Equals(input.ContentType, "text/csv", StringComparison.OrdinalIgnoreCase) && !string.Equals(input.ContentType, "application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Imports must use a CSV content type.");
    }
}

public sealed class ProfileImportJob(GiddyEduDbContext db, ITenantContextSetter tenant, IFileObjectStorage storage, IImportErrorReportWriter reports, IClock clock, ILogger<ProfileImportJob> logger)
{
    private static readonly string[] StudentHeaders = ["AdmissionNumber", "FirstName", "LastName", "DateOfBirth", "Status"];
    private static readonly string[] GuardianHeaders = ["FirstName", "LastName", "Phone", "Email"];

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
                var rows = DataPortabilityService.ParseCsv(await storage.ReadTextAsync(file.ObjectKey, 5 * 1024 * 1024, ct));
                var errors = operation.ImportType switch
                {
                    "Students" => await ImportStudentsAsync(rows, tenantId, ct),
                    "Guardians" => await ImportGuardiansAsync(rows, tenantId, ct),
                    _ => ["The import type is unsupported."]
                };
                if (errors.Count > 0) { DetachAddedProfiles(); var reportId = await reports.WriteAsync(operation, errors, ct); operation.Fail(Math.Max(0, rows.Count - 1), errors.Count, string.Join(" ", errors.Take(20)), clock.UtcNow, reportId); await db.SaveChangesAsync(ct); return; }
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                operation.Complete(Math.Max(0, rows.Count - 1), Math.Max(0, rows.Count - 1), clock.UtcNow); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Profile import {ImportOperationId} failed for tenant {TenantId}.", operationId, tenantId); DetachAddedProfiles();
                operation.Fail(operation.TotalRows, operation.RejectedRows, "The import could not be processed. Verify the CSV format and try again.", clock.UtcNow); await db.SaveChangesAsync(ct);
            }
        }
        finally { tenant.Clear(); }
    }

    private async Task<IReadOnlyList<string>> ImportStudentsAsync(IReadOnlyList<IReadOnlyList<string>> rows, Guid tenantId, CancellationToken ct)
    {
        if (rows.Count == 0) return ["The CSV is empty."];
        if (!rows[0].SequenceEqual(StudentHeaders, StringComparer.OrdinalIgnoreCase)) return [$"The header must be: {string.Join(",", StudentHeaders)}."];
        var existing = new HashSet<string>(await db.Students.Select(x => x.AdmissionNumber).ToListAsync(ct), StringComparer.OrdinalIgnoreCase); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var errors = new List<string>();
        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index]; var number = index + 1;
            if (row.Count != StudentHeaders.Length || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]) || string.IsNullOrWhiteSpace(row[2])) { errors.Add($"Row {number} is missing required values or has an incorrect number of columns."); continue; }
            if (!DateOnly.TryParseExact(row[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate)) { errors.Add($"Row {number} has an invalid date of birth."); continue; }
            var admissionNumber = row[0].Trim().ToUpperInvariant();
            if (admissionNumber.Length > 50 || row[1].Trim().Length > 100 || row[2].Trim().Length > 100) { errors.Add($"Row {number} contains a value that exceeds its permitted length."); continue; }
            if (!string.IsNullOrWhiteSpace(row[4]) && !string.Equals(row[4], "Active", StringComparison.OrdinalIgnoreCase)) { errors.Add($"Row {number} has an unsupported initial status."); continue; }
            if (!seen.Add(admissionNumber) || existing.Contains(admissionNumber)) { errors.Add($"Row {number} has a duplicate admission number."); continue; }
            db.Students.Add(new Student(Guid.NewGuid(), tenantId, admissionNumber, row[1], row[2], birthDate, null, clock.UtcNow));
        }
        return errors;
    }

    private async Task<IReadOnlyList<string>> ImportGuardiansAsync(IReadOnlyList<IReadOnlyList<string>> rows, Guid tenantId, CancellationToken ct)
    {
        if (rows.Count == 0) return ["The CSV is empty."];
        if (!rows[0].SequenceEqual(GuardianHeaders, StringComparer.OrdinalIgnoreCase)) return [$"The header must be: {string.Join(",", GuardianHeaders)}."];
        var existing = new HashSet<string>(await db.Guardians.Select(x => x.Phone).ToListAsync(ct), StringComparer.OrdinalIgnoreCase); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var errors = new List<string>();
        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index]; var number = index + 1;
            if (row.Count != GuardianHeaders.Length || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]) || string.IsNullOrWhiteSpace(row[2])) { errors.Add($"Row {number} is missing required values or has an incorrect number of columns."); continue; }
            var phone = row[2].Trim();
            if (row[0].Trim().Length > 100 || row[1].Trim().Length > 100 || phone.Length > 30 || row[3].Trim().Length > 320) { errors.Add($"Row {number} contains a value that exceeds its permitted length."); continue; }
            if (!string.IsNullOrWhiteSpace(row[3]) && !MailAddress.TryCreate(row[3], out _)) { errors.Add($"Row {number} has an invalid email address."); continue; }
            if (!seen.Add(phone) || existing.Contains(phone)) { errors.Add($"Row {number} has a duplicate phone number."); continue; }
            db.Guardians.Add(new Guardian(Guid.NewGuid(), tenantId, row[0], row[1], phone, string.IsNullOrWhiteSpace(row[3]) ? null : row[3], clock.UtcNow));
        }
        return errors;
    }

    private void DetachAddedProfiles()
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added && (x.Entity is Student || x.Entity is Guardian))) entry.State = EntityState.Detached;
    }
}
