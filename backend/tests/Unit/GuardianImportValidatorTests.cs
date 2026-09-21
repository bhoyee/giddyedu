using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.StudentLifecycle;
using GiddyEdu.Modules.Academics.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.UnitTests;

public sealed class GuardianImportValidatorTests
{
    [Fact]
    public async Task RepeatedGuardianCanLinkTwoChildren_ButDuplicateLinkIsRejected()
    {
        var tenantId = Guid.NewGuid(); var campusId = Guid.NewGuid(); var yearId = Guid.NewGuid();
        var sectionId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var context = new TenantContextAccessor(); context.Set(tenantId, campusId);
        await using var db = new GiddyEduDbContext(new DbContextOptionsBuilder<GiddyEduDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, context);
        db.ClassSections.Add(new ClassSection(sectionId, tenantId, campusId, yearId, Guid.NewGuid(), "JSS 1 A", "JSS1-A", 30, now));
        foreach (var (number, first) in new[] { ("A-001", "Ayo"), ("A-002", "Bisi") })
        {
            var id = Guid.NewGuid();
            db.Students.Add(new Student(id, tenantId, number, first, "Student", new DateOnly(2014, 1, 1), null, now));
            db.Enrollments.Add(new Enrollment(Guid.NewGuid(), tenantId, id, yearId, sectionId, new DateOnly(2026, 9, 1), now));
        }
        await db.SaveChangesAsync();
        IReadOnlyList<IReadOnlyList<string>> rows = [
            GuardianImportFile.Headers,
            ["A-001", "Ada", "Parent", "09096735531", "ada@example.test", "School Road", "Mother", "Yes", "Yes", "Yes"],
            ["A-002", "Ada", "Parent", "09096735531", "ada@example.test", "School Road", "Mother", "Yes", "Yes", "Yes"],
            ["A-001", "Ada", "Parent", "09096735531", "ada@example.test", "School Road", "Mother", "No", "Yes", "Yes"]
        ];

        var reviewed = await GuardianImportValidator.ValidateAsync(db, rows, campusId, CancellationToken.None);
        Assert.Null(reviewed[0].Error);
        Assert.Null(reviewed[1].Error);
        Assert.Contains("more than once", reviewed[2].Error);
        Assert.Equal("JSS 1 A", reviewed[0].ClassName);
        Assert.All(reviewed.Take(2), row => Assert.NotNull(row.StudentId));

        var otherCampus = await GuardianImportValidator.ValidateAsync(db, rows, Guid.NewGuid(), CancellationToken.None);
        Assert.All(otherCampus, row => Assert.Contains("No active student", row.Error));
    }
}
