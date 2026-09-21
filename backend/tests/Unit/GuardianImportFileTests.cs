using System.Text;
using GiddyEdu.Infrastructure.StudentLifecycle;

namespace GiddyEdu.UnitTests;

public sealed class GuardianImportFileTests
{
    [Theory]
    [InlineData(false, "guardians.csv")]
    [InlineData(true, "guardians.xlsx")]
    public void Template_HasTheSameDataColumnsInCsvAndExcel(bool excel, string fileName)
    {
        var rows = GuardianImportFile.Read(GuardianImportFile.Template(excel), fileName);
        Assert.Equal(GuardianImportFile.Headers, Assert.Single(rows));
    }

    [Fact]
    public void Csv_RejectsMoreThanFiveHundredRelationships()
    {
        var content = string.Join(',', GuardianImportFile.Headers) + "\r\n"
            + string.Concat(Enumerable.Repeat("A-001,Ada,Parent,09096735531,ada@example.test,School Road,Mother,Yes,Yes,Yes\r\n", 501));

        Assert.Throws<ArgumentException>(() => GuardianImportFile.Read(Encoding.UTF8.GetBytes(content), "guardians.csv"));
    }

    [Fact]
    public void Excel_RejectsFormulas()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.AddWorksheet("Guardians");
        for (var index = 0; index < GuardianImportFile.Headers.Length; index++)
            sheet.Cell(1, index + 1).Value = GuardianImportFile.Headers[index];
        sheet.Cell(2, 1).FormulaA1 = "=1+1";
        using var stream = new MemoryStream(); workbook.SaveAs(stream);

        Assert.Throws<ArgumentException>(() => GuardianImportFile.Read(stream.ToArray(), "guardians.xlsx"));
    }
}
