using System.Globalization;
using System.Text;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

public interface IDataPortabilityService
{
    Task<string> ExportApplicantsAsync(Guid actor, CancellationToken ct = default);
    Task<string> ExportStudentsAsync(Guid actor, CancellationToken ct = default);
    Task<string> ExportGuardiansAsync(Guid actor, CancellationToken ct = default);
}

public sealed class DataPortabilityService(GiddyEduDbContext db, IFeatureAccessGuard access) : IDataPortabilityService
{
    private const int MaximumExportRows = 50_000;

    public async Task<string> ExportApplicantsAsync(Guid actor, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.AdmissionsManage, FeatureKeys.Admissions, ct);
        var query = db.Applicants.AsNoTracking().OrderBy(x => x.ApplicationNumber);
        await EnsureSizeAsync(query, ct); var output = Header("ApplicationNumber", "FirstName", "LastName", "DateOfBirth", "Email", "Phone", "PreviousSchool", "Source", "Status");
        await foreach (var row in query.Select(x => new { x.ApplicationNumber, x.FirstName, x.LastName, x.DateOfBirth, x.Email, x.Phone, x.PreviousSchool, x.Source, x.Status }).AsAsyncEnumerable().WithCancellation(ct))
            Append(output, row.ApplicationNumber, row.FirstName, row.LastName, row.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row.Email, row.Phone, row.PreviousSchool, row.Source, row.Status.ToString());
        return output.ToString();
    }

    public async Task<string> ExportStudentsAsync(Guid actor, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
        var query = db.Students.AsNoTracking().OrderBy(x => x.AdmissionNumber);
        await EnsureSizeAsync(query, ct); var output = Header("AdmissionNumber", "FirstName", "LastName", "DateOfBirth", "Email", "Status");
        await foreach (var row in query.Select(x => new { x.AdmissionNumber, x.FirstName, x.LastName, x.DateOfBirth, x.Email, x.Status }).AsAsyncEnumerable().WithCancellation(ct))
            Append(output, row.AdmissionNumber, row.FirstName, row.LastName, row.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row.Email, row.Status.ToString());
        return output.ToString();
    }

    public async Task<string> ExportGuardiansAsync(Guid actor, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);
        var query = db.Guardians.AsNoTracking().OrderBy(x => x.LastName).ThenBy(x => x.FirstName);
        await EnsureSizeAsync(query, ct); var output = Header("FirstName", "LastName", "Phone", "Email");
        await foreach (var row in query.Select(x => new { x.FirstName, x.LastName, x.Phone, x.Email }).AsAsyncEnumerable().WithCancellation(ct)) Append(output, row.FirstName, row.LastName, row.Phone, row.Email);
        return output.ToString();
    }

    public static string EscapeCsvCell(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && safe[0] is '=' or '+' or '-' or '@' or '\t' or '\r') safe = $"'{safe}";
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }

    public static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string csv, int maximumRows = 10_000, int maximumColumns = 50)
    {
        ArgumentNullException.ThrowIfNull(csv);
        if (maximumRows <= 0 || maximumColumns <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRows));
        var rows = new List<IReadOnlyList<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var value = csv[index];
            if (quoted)
            {
                if (value == '"' && index + 1 < csv.Length && csv[index + 1] == '"') { cell.Append('"'); index++; }
                else if (value == '"') quoted = false;
                else cell.Append(value);
                continue;
            }
            if (value == '"' && cell.Length == 0) { quoted = true; continue; }
            if (value == ',') { AddCell(); continue; }
            if (value is '\r' or '\n')
            {
                if (value == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n') index++;
                AddCell(); AddRow(); continue;
            }
            cell.Append(value);
        }
        if (quoted) throw new FormatException("The CSV contains an unterminated quoted field.");
        if (cell.Length > 0 || row.Count > 0) { AddCell(); AddRow(); }
        return rows;

        void AddCell() { if (row.Count >= maximumColumns) throw new FormatException($"CSV files are limited to {maximumColumns} columns."); row.Add(cell.ToString().TrimStart('\uFEFF')); cell.Clear(); }
        void AddRow() { if (row.All(string.IsNullOrWhiteSpace)) { row.Clear(); return; } if (rows.Count >= maximumRows + 1) throw new FormatException($"CSV imports are limited to {maximumRows} data rows."); rows.Add(row.ToArray()); row.Clear(); }
    }

    private static StringBuilder Header(params string[] values) { var output = new StringBuilder(); Append(output, values); return output; }
    private static void Append(StringBuilder output, params string?[] values) => output.AppendJoin(',', values.Select(EscapeCsvCell)).Append("\r\n");
    private static async Task EnsureSizeAsync<T>(IQueryable<T> query, CancellationToken ct) { if (await query.Take(MaximumExportRows + 1).CountAsync(ct) > MaximumExportRows) throw new InvalidOperationException($"Exports are limited to {MaximumExportRows} records. Narrow the dataset or use a scheduled export."); }
}
