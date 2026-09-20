using System.Globalization;
using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Identity;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Infrastructure.StudentLifecycle;
using GiddyEdu.Modules.Hr.Domain;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.Modules.Subscriptions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiddyEdu.Infrastructure.Hr;

public sealed record StaffImportPreview(Guid OperationId, int TotalRows, int ValidRows, int RejectedRows, IReadOnlyList<StaffImportPreviewRow> Rows);
public sealed record StaffImportPreviewRow(int Row, string FirstName, string LastName, string Category, string? Position, string? Error);
public sealed record StaffImportOperationInfo(Guid Id, string? Category, ImportOperationStatus Status, int TotalRows,
    int ImportedRows, int RejectedRows, string? ErrorSummary, Guid? ErrorFileId, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc);
public sealed record StaffImportUploadInput(string FileName, string ContentType, long SizeBytes, StaffCategory Category);
public sealed class StaffImportValidationException(string message) : Exception(message);

public interface IStaffImportService
{
    Task<ImportUploadInfo> BeginAsync(Guid actor, StaffImportUploadInput input, CancellationToken ct = default);
    Task UploadContentAsync(Guid actor, Guid operationId, Stream content, string contentType, long? contentLength, CancellationToken ct = default);
    Task<StaffImportPreview> PreviewAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default);
    Task QueueAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<StaffImportOperationInfo> GetAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<IReadOnlyList<StaffImportOperationInfo>> ListAsync(Guid actor, CancellationToken ct = default);
    Task DeleteDraftAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<string> GetErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default);
    Task<byte[]> TemplateAsync(Guid actor, bool excel, StaffCategory category, CancellationToken ct = default);
}

public sealed class StaffImportService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IFileService files,
    IFileObjectStorage storage, IClock clock, IBackgroundJobClient jobs, IStaffService staff, IPermissionService permissions) : IStaffImportService
{
    private const long MaximumBytes = 5 * 1024 * 1024;
    private const string ImportType = "Staff";

    public async Task<ImportUploadInfo> BeginAsync(Guid actor, StaffImportUploadInput input, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        EnsureCategory(input.Category);
        var campusId = RequireCampus();
        var extension = Path.GetExtension(input.FileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".xlsx" || input.SizeBytes is <= 0 or > MaximumBytes)
            throw new ArgumentException("Select a CSV or Excel (.xlsx) file no larger than 5 MB.");
        if (extension == ".csv" && input.ContentType is not ("text/csv" or "text/plain" or "application/csv" or "application/vnd.ms-excel" or "application/octet-stream") ||
            extension == ".xlsx" && input.ContentType is not ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or "application/zip" or "application/octet-stream"))
            throw new ArgumentException("The file type does not match its extension.");
        var operation = new ImportOperation(Guid.NewGuid(), RequireTenant(), ImportType, actor, clock.UtcNow, campusId);
        operation.SetStaffCategory((int)input.Category);
        db.ImportOperations.Add(operation);
        await db.SaveChangesAsync(ct);
        var upload = await files.BeginUploadAsync(input.FileName, input.ContentType, input.SizeBytes, "imports", "StaffImport", operation.Id, actor, ct);
        return new(operation.Id, upload.FileId, upload.UploadUrl);
    }

    public async Task UploadContentAsync(Guid actor, Guid operationId, Stream content, string contentType, long? contentLength, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        var operation = await FindOperationAsync(operationId, ct);
        if (operation.Status != ImportOperationStatus.AwaitingUpload) throw new InvalidOperationException("This staff import has already been submitted.");
        var file = await FindFileAsync(operationId, ct);
        if (file.Status != StoredFileStatus.PendingUpload) throw new InvalidOperationException("Only a pending staff import can receive a file.");
        await files.UploadContentAsync(file.Id, content, contentType, contentLength, ct);
    }

    public async Task<StaffImportPreview> PreviewAsync(Guid actor, Guid operationId, string checksum, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        var operation = await FindOperationAsync(operationId, ct);
        if (operation.Status != ImportOperationStatus.AwaitingUpload) throw new InvalidOperationException("This import has already been submitted.");
        var file = await FindFileAsync(operationId, ct);
        if (file.Status == StoredFileStatus.PendingUpload) await files.CompleteUploadAsync(file.Id, checksum, ct);
        else if (file.Status != StoredFileStatus.Available || !string.Equals(file.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The upload checksum does not match the reviewed file.");
        var rows = await ReadRowsAsync(file, ct);
        await staff.ListPositionsAsync(actor, ct);
        var reviewed = await StaffImportValidator.ValidateAsync(db, rows, RequireCategory(operation), ct);
        return new(operation.Id, reviewed.Count, reviewed.Count(row => row.Error is null), reviewed.Count(row => row.Error is not null),
            reviewed.Select(row => new StaffImportPreviewRow(row.Row, row.FirstName, row.LastName, row.CategoryLabel, row.Position, row.Error)).ToArray());
    }

    public async Task QueueAsync(Guid actor, Guid operationId, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        if (!await permissions.HasPermissionAsync(actor, Permissions.UsersManage, ct))
            throw new UnauthorizedAccessException("Users.Manage permission is required to invite imported staff.");
        var operation = await FindOperationAsync(operationId, ct);
        var file = await FindFileAsync(operationId, ct);
        if (file.Status != StoredFileStatus.Available) throw new InvalidOperationException("Preview the uploaded file before importing staff.");
        await staff.ListPositionsAsync(actor, ct);
        var reviewed = await StaffImportValidator.ValidateAsync(db, await ReadRowsAsync(file, ct), RequireCategory(operation), ct);
        if (reviewed.All(row => row.Error is not null)) throw new InvalidOperationException("No valid staff rows are available to import. Correct the file and upload it again.");
        operation.Queue(file.Id);
        await db.SaveChangesAsync(ct);
        jobs.Enqueue<StaffImportJob>(job => job.ProcessAsync(operation.TenantId, operation.Id, CancellationToken.None));
    }

    public async Task<StaffImportOperationInfo> GetAsync(Guid actor, Guid operationId, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        return await Query().Where(x => x.Id == operationId).Select(x => new StaffImportOperationInfo(x.Id,
            x.StaffCategory == 0 ? "Teaching" : x.StaffCategory == 1 ? "Administrative" : x.StaffCategory == 2 ? "Non-teaching" : null, x.Status, x.TotalRows,
            x.ImportedRows, x.RejectedRows, x.ErrorSummary, x.ErrorFileId, x.CreatedAtUtc, x.CompletedAtUtc)).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Staff import was not found.");
    }

    public async Task<IReadOnlyList<StaffImportOperationInfo>> ListAsync(Guid actor, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        return await Query().OrderByDescending(x => x.CreatedAtUtc).Take(100).Select(x => new StaffImportOperationInfo(x.Id,
            x.StaffCategory == 0 ? "Teaching" : x.StaffCategory == 1 ? "Administrative" : x.StaffCategory == 2 ? "Non-teaching" : null, x.Status,
            x.TotalRows, x.ImportedRows, x.RejectedRows, x.ErrorSummary, x.ErrorFileId, x.CreatedAtUtc, x.CompletedAtUtc)).ToListAsync(ct);
    }

    public async Task DeleteDraftAsync(Guid actor, Guid operationId, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        var operation = await FindOperationAsync(operationId, ct);
        if (operation.Status != ImportOperationStatus.AwaitingUpload)
            throw new InvalidOperationException("Only an awaiting-upload staff import can be deleted. Finished imports can be moved from history instead.");
        var file = await db.StoredFiles.SingleOrDefaultAsync(x => x.EntityType == "StaffImport" && x.EntityId == operationId, ct);
        if (file is not null)
        {
            await files.DeleteAsync(file.Id, ct);
            db.StoredFiles.Remove(file);
        }
        db.ImportOperations.Remove(operation);
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> GetErrorDownloadAsync(Guid actor, Guid operationId, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        var fileId = await Query().Where(x => x.Id == operationId).Select(x => x.ErrorFileId).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Staff import error report was not found.");
        return await files.CreateDownloadUrlAsync(fileId, ct);
    }

    public async Task<byte[]> TemplateAsync(Guid actor, bool excel, StaffCategory category, CancellationToken ct = default)
    {
        await DemandAsync(actor, ct);
        EnsureCategory(category);
        var positions = (await staff.ListPositionsAsync(actor, ct)).Where(position => position.IsActive && position.Category == category)
            .Select(position => position.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToArray();
        return excel ? StaffImportFile.CreateExcelTemplate(category, positions) : StaffImportFile.CreateCsvTemplate(category, positions);
    }

    private static void EnsureCategory(StaffCategory category)
    { if (!Enum.IsDefined(category)) throw new ArgumentException("Select Teaching, Administrative or Non-teaching before downloading or uploading a template."); }
    private static StaffCategory RequireCategory(ImportOperation operation)
    { if (operation.StaffCategory is not { } value || !Enum.IsDefined((StaffCategory)value)) throw new InvalidOperationException("The staff import has no valid category."); return (StaffCategory)value; }

    private IQueryable<ImportOperation> Query() => db.ImportOperations.AsNoTracking().Where(x => x.ImportType == ImportType && x.CampusId == RequireCampus());
    private async Task<ImportOperation> FindOperationAsync(Guid id, CancellationToken ct) =>
        await db.ImportOperations.SingleOrDefaultAsync(x => x.Id == id && x.ImportType == ImportType && x.CampusId == RequireCampus(), ct)
            ?? throw new KeyNotFoundException("Staff import was not found.");
    private async Task<StoredFile> FindFileAsync(Guid operationId, CancellationToken ct) =>
        await db.StoredFiles.SingleOrDefaultAsync(x => x.EntityType == "StaffImport" && x.EntityId == operationId, ct)
            ?? throw new KeyNotFoundException("Staff import file was not found.");
    private async Task<IReadOnlyList<IReadOnlyList<string>>> ReadRowsAsync(StoredFile file, CancellationToken ct) =>
        StaffImportFile.Read(await storage.ReadBytesAsync(file.ObjectKey, MaximumBytes, ct), file.OriginalFileName);
    private Task DemandAsync(Guid actor, CancellationToken ct) => access.DemandAsync(actor, Permissions.StaffManage, FeatureKeys.StaffManagement, ct);
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    private Guid RequireCampus() => tenant.CampusId ?? throw new InvalidOperationException("Select an active campus before importing staff.");
}

public sealed class StaffImportJob(GiddyEduDbContext db, ITenantContextSetter tenant, IFileObjectStorage storage, IImportErrorReportWriter reports,
    IStaffService staff, IAccountInvitationService invitations, IClock clock, ILogger<StaffImportJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ProcessAsync(Guid tenantId, Guid operationId, CancellationToken ct = default)
    {
        tenant.Set(tenantId, null);
        try
        {
            var operation = await db.ImportOperations.SingleAsync(x => x.Id == operationId && x.ImportType == "Staff", ct);
            if (operation.Status == ImportOperationStatus.Completed) return;
            if (operation.Status != ImportOperationStatus.Queued || !operation.CampusId.HasValue) throw new InvalidOperationException("Staff import is not queued for a campus.");
            tenant.Set(tenantId, operation.CampusId);
            operation.Start(clock.UtcNow);
            await db.SaveChangesAsync(ct);
            try
            {
                var file = await db.StoredFiles.AsNoTracking().SingleAsync(x => x.Id == operation.SourceFileId && x.Status == StoredFileStatus.Available, ct);
                var rows = StaffImportFile.Read(await storage.ReadBytesAsync(file.ObjectKey, 5 * 1024 * 1024, ct), file.OriginalFileName);
                await staff.ListPositionsAsync(operation.RequestedByUserId, ct);
                if (operation.StaffCategory is not { } categoryValue || !Enum.IsDefined((StaffCategory)categoryValue))
                    throw new InvalidOperationException("The staff import has no valid category.");
                var reviewed = await StaffImportValidator.ValidateAsync(db, rows, (StaffCategory)categoryValue, ct);
                var errors = reviewed.Where(row => row.Error is not null).Select(row => $"Row {row.Row}: {row.Error}").ToList();
                var imported = 0;
                var importedStaffIds = new List<Guid>();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                foreach (var row in reviewed.Where(row => row.Error is null))
                {
                    try
                    {
                        var staffId = await staff.CreateAsync(operation.RequestedByUserId, new StaffInput(null, row.FirstName, row.LastName, row.Category,
                            operation.CampusId.Value, null, row.PositionId, row.Email, row.Phone, row.HireDate!.Value,
                            MiddleName: row.MiddleName, Gender: row.Gender), ct);
                        importedStaffIds.Add(staffId);
                        imported++;
                    }
                    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateException)
                    {
                        foreach (var entry in db.ChangeTracker.Entries<StaffProfile>().Where(entry => entry.State == EntityState.Added)) entry.State = EntityState.Detached;
                        foreach (var entry in db.ChangeTracker.Entries<StaffSensitiveRecord>().Where(entry => entry.State == EntityState.Added)) entry.State = EntityState.Detached;
                        errors.Add($"Row {row.Row}: {(exception is DbUpdateException ? "The record conflicts with existing staff data." : exception.Message)}");
                    }
                }
                // Keep staff creation and invitation outbox entries in one transaction: no rejected row is invited.
                foreach (var staffId in importedStaffIds)
                    await invitations.CreateAsync(operation.RequestedByUserId, new CreateAccountInvitationInput(InvitationTargetType.Staff, staffId), ct);
                Guid? errorFileId = null;
                if (errors.Count > 0) errorFileId = await reports.WriteAsync(operation, errors, ct);
                if (imported == 0) operation.Fail(reviewed.Count, errors.Count, "No staff records were imported. Download the error report and correct the file.", clock.UtcNow, errorFileId);
                else operation.Complete(reviewed.Count, imported, clock.UtcNow, errors.Count, errorFileId);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Staff import {ImportOperationId} failed for tenant {TenantId}.", operationId, tenantId);
                operation.Fail(operation.TotalRows, operation.RejectedRows, "The staff import could not be completed. Check the template and retry.", clock.UtcNow);
                await db.SaveChangesAsync(ct);
            }
        }
        finally { tenant.Clear(); }
    }
}

internal sealed record StaffImportRow(int Row, string FirstName, string MiddleName, string LastName, string Gender, string Email,
    string Phone, string CategoryLabel, StaffCategory Category, string Position, Guid? PositionId, DateOnly? HireDate, string? Error);

internal static class StaffImportValidator
{
    internal static readonly string[] Headers = ["FirstName", "MiddleName", "LastName", "Gender", "Phone", "Email", "Position", "HireDate"];
    internal static readonly string[] Genders = ["Male", "Female", "Other", "Prefer not to say"];

    public static async Task<IReadOnlyList<StaffImportRow>> ValidateAsync(GiddyEduDbContext db, IReadOnlyList<IReadOnlyList<string>> rows,
        StaffCategory category, CancellationToken ct)
    {
        if (rows.Count == 0 || rows[0].Count < Headers.Length || !rows[0].Take(Headers.Length).SequenceEqual(Headers, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"The first row must be: {string.Join(",", Headers)}.");
        var dataRows = rows.Skip(1).Count(row => row.Take(Headers.Length).Any(value => !string.IsNullOrWhiteSpace(value)));
        if (dataRows == 0) throw new StaffImportValidationException("The file contains no staff records. Fill in the template and try again.");
        if (dataRows > StaffImportFile.MaximumStaffRows)
            throw new StaffImportValidationException($"This file contains {dataRows} staff records. Upload at most {StaffImportFile.MaximumStaffRows} per file; split larger lists into separate imports.");
        var existingEmails = new HashSet<string>(await db.StaffProfiles.Where(x => x.WorkEmail != null).Select(x => x.WorkEmail!).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var existingPhones = new HashSet<string>(await db.StaffProfiles.Where(x => x.Phone != null).Select(x => x.Phone!).ToListAsync(ct), StringComparer.Ordinal);
        var positions = await db.Positions.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Id, x.Name, x.Category }).ToListAsync(ct);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPhones = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<StaffImportRow>();
        for (var index = 1; index < rows.Count; index++)
        {
            var cells = rows[index]; var errors = new List<string>();
            if (cells.Take(Headers.Length).All(string.IsNullOrWhiteSpace)) continue;
            if (cells.Count < Headers.Length) { result.Add(new(index + 1, "", "", "", "", "", "", StaffCategoryLabel(category), category, "", null, null, "Incorrect number of columns.")); continue; }
            var first = cells[0].Trim(); var middle = cells[1].Trim(); var last = cells[2].Trim();
            var gender = Genders.FirstOrDefault(value => value.Equals(cells[3].Trim(), StringComparison.OrdinalIgnoreCase));
            var phone = cells[4].Trim(); var email = cells[5].Trim().ToLowerInvariant(); var positionName = cells[6].Trim();
            if (positionName.Length > 1 && positionName[0] == '\'' && "=+-@".Contains(positionName[1])) positionName = positionName[1..];
            if (first.Length is 0 or > 100 || last.Length is 0 or > 100 || middle.Length > 100)
                errors.Add("First and last names are required; names must be at most 100 characters.");
            if (gender is null) errors.Add("Gender must match a value in the keys area.");
            try { if (StaffFieldNormalization.Email(email) is null) errors.Add("Enter a valid email address."); }
            catch (ArgumentException) { errors.Add("Enter a valid email address."); }
            try { if (StaffFieldNormalization.Phone(phone) is null) errors.Add("Phone must contain exactly 11 digits."); }
            catch (ArgumentException) { errors.Add("Phone must contain exactly 11 digits."); }
            var position = positions.FirstOrDefault(item => item.Name.Equals(positionName, StringComparison.OrdinalIgnoreCase) && item.Category == category);
            if (positionName.Length == 0 || position is null) errors.Add("Choose an existing active position in the selected category.");
            if (!DateOnly.TryParseExact(cells[7].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hireDate)) errors.Add("HireDate must use YYYY-MM-DD.");
            if (email.Length > 0 && (seenEmails.Contains(email) || existingEmails.Contains(email))) errors.Add("Email already exists in this file or school.");
            if (phone.Length > 0 && (seenPhones.Contains(phone) || existingPhones.Contains(phone))) errors.Add("Phone already exists in this file or school.");
            if (errors.Count == 0) { seenEmails.Add(email); seenPhones.Add(phone); }
            result.Add(new(index + 1, first, middle, last, gender ?? "", email, phone, StaffCategoryLabel(category), category, positionName, position?.Id,
                hireDate == default ? null : hireDate, errors.Count == 0 ? null : string.Join(" ", errors)));
        }
        return result;
    }

    internal static string StaffCategoryLabel(StaffCategory category) => category switch
    { StaffCategory.Teaching => "Teaching", StaffCategory.Administrative => "Administrative", _ => "Non-teaching" };
}

internal static class StaffImportFile
{
    internal const int MaximumStaffRows = 500;
    private const int MaximumWorksheetRows = 10_001;

    public static IReadOnlyList<IReadOnlyList<string>> Read(byte[] bytes, string fileName)
    {
        if (Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            try { return DataPortabilityService.ParseCsv(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'), MaximumWorksheetRows - 1, 11); }
            catch (FormatException) { throw new StaffImportValidationException("The CSV could not be read. Use the current staff template and keep its columns unchanged."); }
        }
        using var input = new MemoryStream(bytes);
        try
        {
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, true);
            if (archive.Entries.Count > 150 || archive.Entries.Sum(entry => entry.Length) > 30 * 1024 * 1024)
                throw new StaffImportValidationException("The Excel file is too large to process. Use the current .xlsx template and split the records into smaller files.");
        }
        catch (InvalidDataException) { throw new StaffImportValidationException("The Excel file could not be read. Save it as a valid .xlsx file and try again."); }
        input.Position = 0;
        using var workbook = OpenWorkbook(input);
        var sheet = workbook.Worksheets.FirstOrDefault(worksheet => worksheet.Name == "Staff") ?? workbook.Worksheets.First();
        var used = sheet.RangeUsed() ?? throw new StaffImportValidationException("The Excel file is empty. Fill in the staff template and try again.");
        if (used.LastRow().RowNumber() > MaximumWorksheetRows)
            throw new StaffImportValidationException("This Excel worksheet is too large. Upload at most 500 staff records per file and remove unused rows.");
        if (used.LastColumn().ColumnNumber() > 11)
            throw new StaffImportValidationException("The Excel file has extra columns. Use columns A–H for staff and J–K for the reference keys.");
        var rows = new List<IReadOnlyList<string>>();
        for (var row = 1; row <= used.LastRow().RowNumber(); row++)
        {
            var values = new string[8];
            for (var column = 1; column <= 8; column++)
            {
                var cell = sheet.Cell(row, column);
                if (cell.HasFormula) throw new StaffImportValidationException($"Row {row} contains a formula. Enter values only.");
                values[column - 1] = cell.GetFormattedString().Trim();
            }
            rows.Add(values);
        }
        return rows;
    }

    private static XLWorkbook OpenWorkbook(Stream stream)
    {
        try { return new XLWorkbook(stream); }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or ArgumentException)
        { throw new StaffImportValidationException("The Excel file could not be read. Save it as a valid .xlsx file and try again."); }
    }

    public static byte[] CreateCsvTemplate(StaffCategory category, IReadOnlyList<string> positions)
    {
        var output = new StringBuilder("\uFEFF");
        var count = Math.Max(positions.Count, StaffImportValidator.Genders.Length);
        AppendCsvRow([.. StaffImportValidator.Headers, "", $"Position keys — {StaffImportValidator.StaffCategoryLabel(category)}", "Gender keys"]);
        for (var index = 0; index < count; index++)
            AppendCsvRow(["", "", "", "", "", "", "", "", "", index < positions.Count ? SafeSpreadsheetText(positions[index]) : "",
                index < StaffImportValidator.Genders.Length ? StaffImportValidator.Genders[index] : ""]);
        return Encoding.UTF8.GetBytes(output.ToString());

        void AppendCsvRow(IEnumerable<string> values) => output.AppendJoin(',', values.Select(value =>
            $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"")).Append("\r\n");
    }

    public static byte[] CreateExcelTemplate(StaffCategory category, IReadOnlyList<string> positions)
    {
        using var workbook = new XLWorkbook();
        var staff = workbook.AddWorksheet("Staff");
        var headers = StaffImportValidator.Headers;
        for (var column = 0; column < headers.Length; column++) staff.Cell(1, column + 1).Value = headers[column];
        staff.Cell(1, 10).Value = $"Position keys — {StaffImportValidator.StaffCategoryLabel(category)}";
        staff.Cell(1, 11).Value = "Gender keys";
        for (var index = 0; index < positions.Count; index++) staff.Cell(index + 2, 10).Value = SafeSpreadsheetText(positions[index]);
        for (var index = 0; index < StaffImportValidator.Genders.Length; index++)
            staff.Cell(index + 2, 11).Value = StaffImportValidator.Genders[index];
        staff.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        staff.Range(1, 10, 1, 11).Style.Font.Bold = true;
        staff.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#163B35");
        staff.Range(1, 1, 1, headers.Length).Style.Font.FontColor = XLColor.White;
        staff.Range(1, 10, 1, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#E9F4EF");
        staff.Column(5).Style.NumberFormat.Format = "@";
        staff.Column(8).Style.NumberFormat.Format = "@";
        staff.SheetView.FreezeRows(1);
        staff.Columns().AdjustToContents();
        var guide = workbook.AddWorksheet("Instructions");
        guide.Cell(1, 1).Value = $"GiddyEdu staff import — {StaffImportValidator.StaffCategoryLabel(category)}";
        guide.Cell(2, 1).Value = "Enter one staff member per row on the Staff sheet. Keep the first eight headers unchanged.";
        guide.Cell(3, 1).Value = "Use a position from column J and a gender from column K. Position keys reflect your school's current active positions.";
        guide.Cell(4, 1).Value = "Phone: 11 digits, including the leading zero. HireDate: YYYY-MM-DD.";
        guide.Cell(5, 1).Value = "The selected category and active campus apply to every row. Leave columns I–K for reference keys.";
        guide.Cell(6, 1).Value = "Add photos, signatures, qualifications and documents to individual staff profiles after import.";
        guide.Columns().AdjustToContents();
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static string SafeSpreadsheetText(string value) => value.Length > 0 && "=+-@".Contains(value[0]) ? $"'{value}" : value;
}
