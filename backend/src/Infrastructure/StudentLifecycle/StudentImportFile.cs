using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

internal static class StudentImportFile
{
    internal const int MaximumRows = 500;
    internal static readonly string[] Headers = ["AdmissionNumber", "FirstName", "LastName", "DateOfBirth", "Email", "Status"];

    internal static IReadOnlyList<IReadOnlyList<string>> Read(byte[] bytes, string fileName)
    {
        if (Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return DataPortabilityService.ParseCsv(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'), MaximumRows, Headers.Length);
        if (!Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a CSV or Excel (.xlsx) file.");
        using var input = new MemoryStream(bytes);
        using (var archive = new ZipArchive(input, ZipArchiveMode.Read, true))
            if (archive.Entries.Count > 150 || archive.Entries.Sum(entry => entry.Length) > 30 * 1024 * 1024) throw new ArgumentException("The Excel file is too large.");
        input.Position = 0; using var workbook = new XLWorkbook(input); var sheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "Students") ?? workbook.Worksheets.First();
        var used = sheet.RangeUsed() ?? throw new ArgumentException("The Excel file is empty.");
        if (used.LastRow().RowNumber() > MaximumRows + 1 || used.LastColumn().ColumnNumber() > Headers.Length) throw new ArgumentException($"The template supports at most {MaximumRows} students.");
        var rows = new List<IReadOnlyList<string>>();
        for (var row = 1; row <= used.LastRow().RowNumber(); row++) { var values = new string[Headers.Length]; for (var column = 1; column <= Headers.Length; column++) { var cell = sheet.Cell(row, column); if (cell.HasFormula) throw new ArgumentException($"Row {row} contains a formula. Enter values only."); values[column - 1] = cell.GetFormattedString().Trim(); } rows.Add(values); }
        return rows;
    }

    internal static byte[] Template(bool excel)
    {
        if (!excel) return Encoding.UTF8.GetBytes("\uFEFF" + string.Join(',', Headers.Select(DataPortabilityService.EscapeCsvCell)) + "\r\n");
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Students");
        for (var index = 0; index < Headers.Length; index++) sheet.Cell(1, index + 1).Value = Headers[index];
        sheet.Range(1, 1, 1, Headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#163B35"); sheet.Range(1, 1, 1, Headers.Length).Style.Font.FontColor = XLColor.White; sheet.Range(1, 1, 1, Headers.Length).Style.Font.Bold = true;
        sheet.Column(1).Style.NumberFormat.Format = "@"; sheet.Column(4).Style.NumberFormat.Format = "yyyy-mm-dd"; sheet.SheetView.FreezeRows(1); sheet.Columns().AdjustToContents();
        var guide=workbook.AddWorksheet("Instructions"); guide.Cell(1,1).Value="GiddyEdu student import"; guide.Cell(2,1).Value="Use one row per student. AdmissionNumber must be unique. DateOfBirth uses YYYY-MM-DD."; guide.Cell(3,1).Value="Status must be Active. Add class placement, guardians, photos and documents after import."; guide.Cell(4,1).Value=$"Upload no more than {MaximumRows} students per file."; guide.Columns().AdjustToContents();
        using var output=new MemoryStream(); workbook.SaveAs(output); return output.ToArray();
    }
}
