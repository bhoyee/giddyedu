using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

internal static class GuardianImportFile
{
    internal const int MaximumRows = 500;
    internal static readonly string[] Headers = ["AdmissionNumber", "FirstName", "LastName", "Phone", "Email", "Address",
        "Relationship", "PrimaryGuardian", "EmergencyContact", "MayCollect"];

    internal static IReadOnlyList<IReadOnlyList<string>> Read(byte[] bytes, string fileName)
    {
        if (Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            try { return DataPortabilityService.ParseCsv(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'), MaximumRows, Headers.Length); }
            catch (FormatException exception) { throw new ArgumentException($"The CSV could not be read. Upload at most {MaximumRows} relationships with the current guardian template. {exception.Message}"); }
        }
        if (!Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a CSV or Excel (.xlsx) file.");
        using var input = new MemoryStream(bytes);
        try
        {
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, true);
            if (archive.Entries.Count > 150 || archive.Entries.Sum(entry => entry.Length) > 30 * 1024 * 1024)
                throw new ArgumentException("The Excel file is too large. Split the list into smaller uploads.");
        }
        catch (InvalidDataException) { throw new ArgumentException("The Excel file is invalid. Save it as .xlsx and try again."); }
        input.Position = 0;
        try
        {
            using var workbook = new XLWorkbook(input);
            var sheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "Guardians") ?? workbook.Worksheets.First();
            var used = sheet.RangeUsed() ?? throw new ArgumentException("The Excel file is empty.");
            if (used.LastRow().RowNumber() > MaximumRows + 1 || used.LastColumn().ColumnNumber() > Headers.Length)
                throw new ArgumentException($"The template supports at most {MaximumRows} rows and {Headers.Length} columns.");
            var rows = new List<IReadOnlyList<string>>();
            for (var row = 1; row <= used.LastRow().RowNumber(); row++)
            {
                var values = new string[Headers.Length];
                for (var column = 1; column <= Headers.Length; column++)
                {
                    var cell = sheet.Cell(row, column);
                    if (cell.HasFormula) throw new ArgumentException($"Row {row} contains a formula. Enter values only.");
                    values[column - 1] = cell.GetFormattedString().Trim();
                }
                rows.Add(values);
            }
            return rows;
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or ArgumentException)
        { throw new ArgumentException("The Excel file could not be read. Use the current guardian template and enter values only.", exception); }
    }

    internal static byte[] Template(bool excel)
    {
        if (!excel)
            return Encoding.UTF8.GetBytes("\uFEFF" + string.Join(',', Headers.Select(DataPortabilityService.EscapeCsvCell)) + "\r\n");
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Guardians");
        for (var index = 0; index < Headers.Length; index++) sheet.Cell(1, index + 1).Value = Headers[index];
        sheet.Range(1, 1, 1, Headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#163B35");
        sheet.Range(1, 1, 1, Headers.Length).Style.Font.FontColor = XLColor.White;
        sheet.Range(1, 1, 1, Headers.Length).Style.Font.Bold = true;
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Column(4).Style.NumberFormat.Format = "@";
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        var guide = workbook.AddWorksheet("Instructions");
        guide.Cell(1, 1).Value = "GiddyEdu guardian import";
        guide.Cell(2, 1).Value = "Enter one guardian-student relationship per row. Repeat a guardian's contact details for each child.";
        guide.Cell(3, 1).Value = "AdmissionNumber must match an active student in the selected campus. Do not use the student's name as a key.";
        guide.Cell(4, 1).Value = "Phone must be 11 digits including the leading zero. Email and address are required.";
        guide.Cell(5, 1).Value = "PrimaryGuardian, EmergencyContact and MayCollect accept Yes or No.";
        guide.Cell(6, 1).Value = "Review matched students and all errors before confirming. Accepted guardians receive one secure invitation each.";
        guide.Cell(8, 1).Value = "Relationship keys";
        foreach (var (name, index) in Enum.GetNames<GiddyEdu.Modules.StudentLifecycle.Domain.GuardianRelationshipType>().Select((name, index) => (name, index)))
            guide.Cell(index + 9, 1).Value = name;
        guide.Columns().AdjustToContents();
        using var output = new MemoryStream(); workbook.SaveAs(output); return output.ToArray();
    }
}
