using System.Security.Cryptography;
using System.Text;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

public interface IImportErrorReportWriter
{
    Task<Guid> WriteAsync(ImportOperation operation, IReadOnlyList<string> errors, CancellationToken ct);
}

public sealed class ImportErrorReportWriter(GiddyEduDbContext db, IObjectKeyFactory keys, IFileObjectStorage storage, IClock clock) : IImportErrorReportWriter
{
    public async Task<Guid> WriteAsync(ImportOperation operation, IReadOnlyList<string> errors, CancellationToken ct)
    {
        if (errors.Count == 0) throw new ArgumentException("At least one import error is required.", nameof(errors));
        var fileId = Guid.NewGuid();
        var output = new StringBuilder("\uFEFF\"Error\"\r\n");
        foreach (var error in errors) output.Append(DataPortabilityService.EscapeCsvCell(error)).Append("\r\n");
        var content = output.ToString();
        var objectKey = keys.Create(operation.TenantId, "import-errors", "ImportOperation", operation.Id, fileId, "csv");
        await storage.WriteTextAsync(objectKey, "text/csv; charset=utf-8", content, ct);
        var file = new StoredFile(fileId, operation.TenantId, objectKey, $"{operation.ImportType.ToLowerInvariant()}-import-errors.csv", "text/csv; charset=utf-8", Encoding.UTF8.GetByteCount(content), "import-errors", "ImportOperation", operation.Id, operation.RequestedByUserId, clock.UtcNow);
        file.MarkAvailable(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
        db.StoredFiles.Add(file);
        return fileId;
    }
}
