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

public sealed record ImportUploadInput(string FileName, string ContentType, long SizeBytes);
public sealed record ImportUploadInfo(Guid OperationId, Guid FileId, string UploadUrl);
public sealed record ImportOperationInfo(Guid Id, string ImportType, ImportOperationStatus Status, int TotalRows, int ImportedRows, int RejectedRows, string? ErrorSummary, Guid? ErrorFileId, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc);

public interface IApplicantImportService
{
    Task<ImportUploadInfo> BeginAsync(Guid actor, ImportUploadInput input, CancellationToken ct = default);
    Task QueueAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default);
    Task<ImportOperationInfo> GetAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<IReadOnlyList<ImportOperationInfo>> ListAsync(Guid actor, CancellationToken ct = default);
    Task<string> GetErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default);
}

public sealed class ApplicantImportService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IFileService files, IClock clock, IBackgroundJobClient jobs) : IApplicantImportService
{
    private const long MaximumImportBytes = 5 * 1024 * 1024;
    public async Task<ImportUploadInfo> BeginAsync(Guid actor, ImportUploadInput input, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.AdmissionsManage, FeatureKeys.Admissions, ct);
        if (!string.Equals(Path.GetExtension(input.FileName), ".csv", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Applicant imports must use a .csv file.");
        if (input.SizeBytes is <= 0 or > MaximumImportBytes) throw new ArgumentOutOfRangeException(nameof(input.SizeBytes), "Applicant imports are limited to 5 MB.");
        if (!string.Equals(input.ContentType, "text/csv", StringComparison.OrdinalIgnoreCase) && !string.Equals(input.ContentType, "application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Applicant imports must use a CSV content type.");
        var operation = new ImportOperation(Guid.NewGuid(), tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required."), "Applicants", actor, clock.UtcNow);
        db.ImportOperations.Add(operation); await db.SaveChangesAsync(ct);
        var upload = await files.BeginUploadAsync(input.FileName, input.ContentType, input.SizeBytes, "imports", "ApplicantImport", operation.Id, actor, ct);
        return new(operation.Id, upload.FileId, upload.UploadUrl);
    }

    public async Task QueueAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.AdmissionsManage, FeatureKeys.Admissions, ct);
        var operation = await db.ImportOperations.SingleOrDefaultAsync(x => x.Id == operationId && x.ImportType == "Applicants", ct) ?? throw new KeyNotFoundException("Import operation was not found.");
        var file = await db.StoredFiles.SingleOrDefaultAsync(x => x.EntityType == "ApplicantImport" && x.EntityId == operationId && x.Status == StoredFileStatus.PendingUpload, ct) ?? throw new KeyNotFoundException("Import upload was not found.");
        await files.CompleteUploadAsync(file.Id, checksum, ct); operation.Queue(file.Id); await db.SaveChangesAsync(ct);
        jobs.Enqueue<ApplicantImportJob>(job => job.ProcessAsync(operation.TenantId, operation.Id, CancellationToken.None));
    }

    public async Task<ImportOperationInfo> GetAsync(Guid actor, Guid operationId, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.AdmissionsManage, FeatureKeys.Admissions, ct);
        return await db.ImportOperations.AsNoTracking().Where(x => x.Id == operationId && x.ImportType == "Applicants")
            .Select(x => new ImportOperationInfo(x.Id, x.ImportType, x.Status, x.TotalRows, x.ImportedRows, x.RejectedRows, x.ErrorSummary, x.ErrorFileId, x.CreatedAtUtc, x.CompletedAtUtc)).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Import operation was not found.");
    }

    public async Task<IReadOnlyList<ImportOperationInfo>> ListAsync(Guid actor, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.AdmissionsManage, FeatureKeys.Admissions, ct);
        return await db.ImportOperations.AsNoTracking().Where(x => x.ImportType == "Applicants").OrderByDescending(x => x.CreatedAtUtc).Take(50).Select(x => new ImportOperationInfo(x.Id, x.ImportType, x.Status, x.TotalRows, x.ImportedRows, x.RejectedRows, x.ErrorSummary, x.ErrorFileId, x.CreatedAtUtc, x.CompletedAtUtc)).ToListAsync(ct);
    }

    public async Task<string> GetErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.AdmissionsManage, FeatureKeys.Admissions, ct);
        var fileId = await db.ImportOperations.Where(x => x.Id == operationId && x.ImportType == "Applicants").Select(x => x.ErrorFileId).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Import error report was not found.");
        return await files.CreateDownloadUrlAsync(fileId, ct);
    }
}

public sealed class ApplicantImportJob(GiddyEduDbContext db, ITenantContextSetter tenant, IFileObjectStorage storage, IImportErrorReportWriter reports, IClock clock, Microsoft.Extensions.Logging.ILogger<ApplicantImportJob> logger)
{
    private static readonly string[] RequiredHeaders = ["ApplicationNumber", "FirstName", "LastName", "DateOfBirth", "Email", "Phone", "PreviousSchool", "Source", "Status"];

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
                var errors = Validate(rows, out var imports);
                if (errors.Count > 0) { var reportId = await reports.WriteAsync(operation, errors, ct); operation.Fail(Math.Max(0, rows.Count - 1), errors.Count, string.Join(" ", errors.Take(20)), clock.UtcNow, reportId); await db.SaveChangesAsync(ct); return; }
                var tenantApplicantNumbers = new HashSet<string>(await db.Applicants.Select(x => x.ApplicationNumber).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
                var duplicates = imports.Where(x => tenantApplicantNumbers.Contains(x.ApplicationNumber)).Select(x => x.Row).Take(20).ToArray();
                if (duplicates.Length > 0) { var duplicateErrors = duplicates.Select(row => $"Row {row} has an application number that already exists.").ToArray(); var reportId = await reports.WriteAsync(operation, duplicateErrors, ct); operation.Fail(imports.Count, duplicates.Length, string.Join(" ", duplicateErrors), clock.UtcNow, reportId); await db.SaveChangesAsync(ct); return; }
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                foreach (var item in imports) db.Applicants.Add(new Applicant(Guid.NewGuid(), tenantId, item.ApplicationNumber, item.FirstName, item.LastName, item.DateOfBirth, item.Email, item.Phone, item.PreviousSchool, item.Source, clock.UtcNow));
                operation.Complete(imports.Count, imports.Count, clock.UtcNow); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Applicant import {ImportOperationId} failed for tenant {TenantId}.", operationId, tenantId);
                foreach (var entry in db.ChangeTracker.Entries<Applicant>().Where(x => x.State == EntityState.Added)) entry.State = EntityState.Detached;
                operation.Fail(operation.TotalRows, operation.RejectedRows, "The import could not be processed. Verify the CSV format and try again.", clock.UtcNow); await db.SaveChangesAsync(ct);
            }
        }
        finally { tenant.Clear(); }
    }

    internal static IReadOnlyList<string> Validate(IReadOnlyList<IReadOnlyList<string>> rows, out List<ApplicantImportRow> imports)
    {
        imports = []; var errors = new List<string>();
        if (rows.Count == 0) return ["The CSV is empty."];
        if (!rows[0].SequenceEqual(RequiredHeaders, StringComparer.OrdinalIgnoreCase)) return [$"The header must be: {string.Join(",", RequiredHeaders)}."];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index]; var number = index + 1;
            if (row.Count != RequiredHeaders.Length) { errors.Add($"Row {number} has an incorrect number of columns."); continue; }
            if (string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]) || string.IsNullOrWhiteSpace(row[2])) { errors.Add($"Row {number} is missing a required value."); continue; }
            if (!DateOnly.TryParseExact(row[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate)) { errors.Add($"Row {number} has an invalid date of birth."); continue; }
            if (!string.IsNullOrWhiteSpace(row[4]) && !MailAddress.TryCreate(row[4], out _)) { errors.Add($"Row {number} has an invalid email address."); continue; }
            if (row[0].Trim().Length > 50 || row[1].Trim().Length > 100 || row[2].Trim().Length > 100 || row[4].Trim().Length > 320 || row[5].Trim().Length > 30 || row[6].Trim().Length > 200 || row[7].Trim().Length > 100) { errors.Add($"Row {number} contains a value that exceeds its permitted length."); continue; }
            if (!string.IsNullOrWhiteSpace(row[8]) && !string.Equals(row[8], "Draft", StringComparison.OrdinalIgnoreCase)) { errors.Add($"Row {number} has an unsupported initial status."); continue; }
            var applicationNumber = row[0].Trim().ToUpperInvariant();
            if (!seen.Add(applicationNumber)) { errors.Add($"Row {number} duplicates an application number in this file."); continue; }
            try { imports.Add(new(number, applicationNumber, row[1], row[2], birthDate, Empty(row[4]), Empty(row[5]), Empty(row[6]), Empty(row[7]))); }
            catch (ArgumentException) { errors.Add($"Row {number} contains a value that exceeds its permitted length."); }
        }
        return errors;
    }
    private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record ApplicantImportRow(int Row, string ApplicationNumber, string FirstName, string LastName, DateOnly DateOfBirth, string? Email, string? Phone, string? PreviousSchool, string? Source);
