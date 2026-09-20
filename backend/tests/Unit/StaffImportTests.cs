using System.Text;
using ClosedXML.Excel;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Hr;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Hr.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.UnitTests;

public sealed class StaffImportTests
{
    [Fact]
    public void ExcelTemplate_ListsOnlySelectedCategoryPositionsAndGenderKeys()
    {
        var bytes = StaffImportFile.CreateExcelTemplate(StaffCategory.Teaching, ["Class Teacher", "Subject Teacher"]);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Staff");

        Assert.Equal("FirstName", sheet.Cell("A1").GetString());
        Assert.Equal("Gender", sheet.Cell("D1").GetString());
        Assert.Equal("Position", sheet.Cell("G1").GetString());
        Assert.Contains("Teaching", sheet.Cell("J1").GetString());
        Assert.Equal("Class Teacher", sheet.Cell("J2").GetString());
        Assert.Equal("Subject Teacher", sheet.Cell("J3").GetString());
        Assert.Equal("Male", sheet.Cell("K2").GetString());
        Assert.Equal("Prefer not to say", sheet.Cell("K5").GetString());
    }

    [Fact]
    public async Task CsvTemplate_ReferenceKeysAreNotImportedAsStaff()
    {
        var tenantId = Guid.NewGuid();
        var context = new TenantContextAccessor(); context.Set(tenantId, Guid.NewGuid());
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context);
        db.Positions.Add(new Position(Guid.NewGuid(), tenantId, "Class Teacher", "CLASS-TEACHER", StaffCategory.Teaching, false, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var template = Encoding.UTF8.GetString(StaffImportFile.CreateCsvTemplate(StaffCategory.Teaching, ["Class Teacher"]));
        var firstBreak = template.IndexOf("\r\n", StringComparison.Ordinal) + 2;
        var staffRow = "Ade,Jo,Salihu,Female,09096735531,ade@example.test,Class Teacher,2026-09-01,,,\r\n";
        var csv = template.Insert(firstBreak, staffRow);
        var rows = StaffImportFile.Read(Encoding.UTF8.GetBytes(csv), "staff.csv");
        var reviewed = await StaffImportValidator.ValidateAsync(db, rows, StaffCategory.Teaching, CancellationToken.None);

        var staff = Assert.Single(reviewed);
        Assert.Null(staff.Error);
        Assert.Equal("Jo", staff.MiddleName);
        Assert.Equal("Female", staff.Gender);
        Assert.Equal(StaffCategory.Teaching, staff.Category);

        var wrongCategory = await StaffImportValidator.ValidateAsync(db, rows, StaffCategory.Administrative, CancellationToken.None);
        Assert.Contains("selected category", Assert.Single(wrongCategory).Error);
    }

    [Fact]
    public async Task ExcelTemplate_ImportsStaffWhileIgnoringReferenceColumns()
    {
        var tenantId = Guid.NewGuid();
        var context = new TenantContextAccessor(); context.Set(tenantId, Guid.NewGuid());
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context);
        db.Positions.Add(new Position(Guid.NewGuid(), tenantId, "School Nurse", "SCHOOL-NURSE", StaffCategory.NonTeaching, false, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        using var workbook = new XLWorkbook(new MemoryStream(StaffImportFile.CreateExcelTemplate(StaffCategory.NonTeaching, ["School Nurse"])));
        var sheet = workbook.Worksheet("Staff");
        var values = new[] { "Amina", "Z", "Bello", "Female", "09096735531", "amina@example.test", "School Nurse", "2026-09-01" };
        for (var column = 0; column < values.Length; column++) sheet.Cell(2, column + 1).Value = values[column];
        using var output = new MemoryStream(); workbook.SaveAs(output);

        var rows = StaffImportFile.Read(output.ToArray(), "staff.xlsx");
        var reviewed = await StaffImportValidator.ValidateAsync(db, rows, StaffCategory.NonTeaching, CancellationToken.None);
        var staff = Assert.Single(reviewed);
        Assert.Null(staff.Error);
        Assert.Equal("School Nurse", staff.Position);
    }

    [Fact]
    public void ImportOperation_RequiresStaffCategoryBeforeQueue()
    {
        var operation = new ImportOperation(Guid.NewGuid(), Guid.NewGuid(), "Staff", Guid.NewGuid(), DateTimeOffset.UtcNow);
        operation.SetStaffCategory((int)StaffCategory.NonTeaching);
        Assert.Equal((int)StaffCategory.NonTeaching, operation.StaffCategory);
        Assert.Throws<ArgumentException>(() => operation.SetStaffCategory(10));
        operation.Queue(Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => operation.SetStaffCategory((int)StaffCategory.Teaching));
    }

    [Fact]
    public void CsvTemplate_ProtectsPositionKeysFromSpreadsheetFormulaExecution()
    {
        var template = Encoding.UTF8.GetString(StaffImportFile.CreateCsvTemplate(StaffCategory.Teaching, ["=HYPERLINK(\"https://example.test\")"]));
        Assert.Contains("'=HYPERLINK", template);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public async Task MoreThanFiveHundredStaffRows_GivesActionableError(string format)
    {
        byte[] bytes;
        if (format == "csv")
        {
            var template = Encoding.UTF8.GetString(StaffImportFile.CreateCsvTemplate(StaffCategory.Teaching, ["Teacher"]));
            bytes = Encoding.UTF8.GetBytes(template + string.Concat(Enumerable.Repeat("Ada,,,,,,,,,,\r\n", 501)));
        }
        else
        {
            using var workbook = new XLWorkbook(new MemoryStream(StaffImportFile.CreateExcelTemplate(StaffCategory.Teaching, ["Teacher"])));
            var sheet = workbook.Worksheet("Staff");
            for (var row = 2; row <= 502; row++) sheet.Cell(row, 1).Value = "Ada";
            using var output = new MemoryStream(); workbook.SaveAs(output);
            bytes = output.ToArray();
        }

        var tenant = new TenantContextAccessor(); tenant.Set(Guid.NewGuid(), Guid.NewGuid());
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, tenant);
        var rows = StaffImportFile.Read(bytes, $"staff.{format}");
        var exception = await Assert.ThrowsAsync<StaffImportValidationException>(() =>
            StaffImportValidator.ValidateAsync(db, rows, StaffCategory.Teaching, CancellationToken.None));
        Assert.Contains("501 staff records", exception.Message);
        Assert.Contains("split larger lists", exception.Message);
    }

    [Fact]
    public async Task TemplateReferenceKeys_DoNotCountTowardFiveHundredStaffRows()
    {
        var positions = Enumerable.Range(1, 510).Select(index => $"Position {index}").ToArray();
        var template = Encoding.UTF8.GetString(StaffImportFile.CreateCsvTemplate(StaffCategory.Teaching, positions));
        var firstBreak = template.IndexOf("\r\n", StringComparison.Ordinal) + 2;
        var csv = template.Insert(firstBreak, "Ada,,Bello,Female,09096735531,ada@example.test,Position 1,2026-09-01,,,\r\n");
        var tenantId = Guid.NewGuid();
        var tenant = new TenantContextAccessor(); tenant.Set(tenantId, Guid.NewGuid());
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, tenant);
        db.Positions.Add(new Position(Guid.NewGuid(), tenantId, "Position 1", "POSITION-1", StaffCategory.Teaching, false, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var rows = StaffImportFile.Read(Encoding.UTF8.GetBytes(csv), "staff.csv");
        var reviewed = await StaffImportValidator.ValidateAsync(db, rows, StaffCategory.Teaching, CancellationToken.None);
        Assert.Single(reviewed);
    }

    [Fact]
    public void InvalidExcelFile_GivesFileFormatError()
    {
        var exception = Assert.Throws<StaffImportValidationException>(() =>
            StaffImportFile.Read(Encoding.UTF8.GetBytes("not an Excel workbook"), "staff.xlsx"));
        Assert.Contains("valid .xlsx", exception.Message);
    }
}
