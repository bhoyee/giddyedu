using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Academics;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Schools;
using GiddyEdu.Infrastructure.Hr;
using GiddyEdu.Modules.Academics.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using GiddyEdu.Modules.Hr.Domain;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Infrastructure.StudentLifecycle;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.SecurityTests;

public sealed class PhaseOneIsolationTests
{
    [Fact]
    public async Task StaffImportHistory_IncludesPreviouslyMovedImports()
    {
        await using var fixture = await Fixture.CreateAsync();
        var actor = Guid.NewGuid();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var operation = new ImportOperation(Guid.NewGuid(), fixture.TenantA, "Staff", actor, fixture.Clock.UtcNow, fixture.CampusA);
        operation.Queue(Guid.NewGuid()); operation.Start(fixture.Clock.UtcNow); operation.Complete(1, 1, fixture.Clock.UtcNow);
        fixture.Db.ImportOperations.Add(operation);
        fixture.Db.Entry(operation).Property(x => x.ArchivedAtUtc).CurrentValue = fixture.Clock.UtcNow;
        await fixture.Db.SaveChangesAsync();
        var service = new StaffImportService(fixture.Db, fixture.Context, new AllowedAccess(), new UnusedFileService(),
            null!, fixture.Clock, null!, null!, null!);

        Assert.Contains(await service.ListAsync(actor), item => item.Id == operation.Id);
    }

    [Fact]
    public async Task StaffImportDraftDelete_RequiresCurrentCampusAndRemovesReservedFile()
    {
        await using var fixture = await Fixture.CreateAsync();
        var actor = Guid.NewGuid(); var operationId = Guid.NewGuid(); var fileId = Guid.NewGuid();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var operation = new ImportOperation(operationId, fixture.TenantA, "Staff", actor, fixture.Clock.UtcNow, fixture.CampusA);
        fixture.Db.ImportOperations.Add(operation);
        fixture.Db.StoredFiles.Add(new StoredFile(fileId, fixture.TenantA, "imports/draft.csv", "draft.csv", "text/csv", 8,
            "imports", "StaffImport", operationId, actor, fixture.Clock.UtcNow));
        await fixture.Db.SaveChangesAsync();
        var files = new RecordingFileService();
        var service = new StaffImportService(fixture.Db, fixture.Context, new AllowedAccess(), files,
            null!, fixture.Clock, null!, null!, null!);

        fixture.Context.Set(fixture.TenantB, fixture.CampusA);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteDraftAsync(actor, operationId));
        fixture.Context.Set(fixture.TenantA, Guid.NewGuid());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteDraftAsync(actor, operationId));
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        await service.DeleteDraftAsync(actor, operationId);
        Assert.Equal(fileId, files.DeletedFileId);
        Assert.False(await fixture.Db.ImportOperations.AnyAsync(x => x.Id == operationId));
        Assert.False(await fixture.Db.StoredFiles.AnyAsync(x => x.Id == fileId));
    }

    [Fact]
    public async Task StaffImportDraftDelete_DoesNotDeleteQueuedImport()
    {
        await using var fixture = await Fixture.CreateAsync();
        var actor = Guid.NewGuid(); var operationId = Guid.NewGuid();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var operation = new ImportOperation(operationId, fixture.TenantA, "Staff", actor, fixture.Clock.UtcNow, fixture.CampusA);
        operation.Queue(Guid.NewGuid());
        fixture.Db.ImportOperations.Add(operation);
        await fixture.Db.SaveChangesAsync();
        var service = new StaffImportService(fixture.Db, fixture.Context, new AllowedAccess(), new RecordingFileService(),
            null!, fixture.Clock, null!, null!, null!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteDraftAsync(actor, operationId));
        Assert.True(await fixture.Db.ImportOperations.AnyAsync(x => x.Id == operationId));
    }

    [Fact]
    public async Task StaffImportContent_RequiresCurrentTenantCampusAndPermission()
    {
        await using var fixture = await Fixture.CreateAsync();
        var actor = Guid.NewGuid(); var operationId = Guid.NewGuid(); var fileId = Guid.NewGuid();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var operation = new GiddyEdu.Modules.StudentLifecycle.Domain.ImportOperation(operationId, fixture.TenantA, "Staff", actor, fixture.Clock.UtcNow, fixture.CampusA);
        operation.SetStaffCategory((int)StaffCategory.Teaching);
        fixture.Db.ImportOperations.Add(operation);
        fixture.Db.StoredFiles.Add(new StoredFile(fileId, fixture.TenantA, "imports/staff.csv", "staff.csv", "text/csv", 8,
            "imports", "StaffImport", operationId, actor, fixture.Clock.UtcNow));
        await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        var service = new StaffImportService(fixture.Db, fixture.Context, new AllowedAccess(), new UnusedFileService(),
            null!, fixture.Clock, null!, null!, null!);
        await using var content = new MemoryStream(new byte[8]);

        fixture.Context.Set(fixture.TenantA, Guid.NewGuid());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UploadContentAsync(actor, operationId, content, "text/csv", 8));
        fixture.Context.Set(fixture.TenantB, Guid.NewGuid());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UploadContentAsync(actor, operationId, content, "text/csv", 8));
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var denied = new StaffImportService(fixture.Db, fixture.Context, new DeniedAccess(), new UnusedFileService(),
            null!, fixture.Clock, null!, null!, null!);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => denied.UploadContentAsync(actor, operationId, content, "text/csv", 8));
    }

    [Fact]
    public async Task AcademicStructure_IsTenantIsolated()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null); fixture.Db.AcademicYears.Add(new AcademicYear(Guid.NewGuid(), fixture.TenantA, "A Year", new(2026, 9, 1), new(2027, 7, 31), fixture.Clock.UtcNow)); await fixture.Db.SaveChangesAsync();
        fixture.Context.Set(fixture.TenantB, null); fixture.Db.AcademicYears.Add(new AcademicYear(Guid.NewGuid(), fixture.TenantB, "B Year", new(2026, 9, 1), new(2027, 7, 31), fixture.Clock.UtcNow)); await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear(); fixture.Context.Set(fixture.TenantA, null);
        var result = await fixture.Academics().GetAsync(Guid.NewGuid());
        Assert.Single(result.AcademicYears); Assert.Equal("A Year", result.AcademicYears.Single().Name);
    }

    [Fact]
    public async Task ClassSection_RejectsCrossTenantReferences()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantB, null);
        var yearB = await fixture.Academics().CreateAcademicYearAsync(Guid.NewGuid(), new("2026/2027", new(2026, 9, 1), new(2027, 7, 31)));
        var stageB = await fixture.Academics().CreateEducationStageAsync(Guid.NewGuid(), new("Junior Secondary", "JSS", 40));
        var levelB = await fixture.Academics().CreateClassLevelAsync(Guid.NewGuid(), new(stageB, "JSS 1", "JSS1", 1));
        fixture.Db.ChangeTracker.Clear(); fixture.Context.Set(fixture.TenantA, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Academics().CreateClassSectionAsync(Guid.NewGuid(), new(levelB, "A", 30)));
    }

    [Fact]
    public async Task Services_RejectActorsWithoutRequiredPermissions()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var deniedAcademics = new AcademicStructureService(fixture.Db, fixture.Context, new DeniedAccess(), fixture.Clock);
        var deniedSchools = new SchoolAdministrationService(fixture.Db, fixture.Context, new DeniedAccess(), new UnusedFileService(), fixture.Clock);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => deniedAcademics.GetAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => deniedSchools.ListCampusesAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SchoolAdministration_PreservesAtLeastOneActiveCampus()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Schools().DeleteCampusAsync(Guid.NewGuid(), fixture.CampusA));
    }

    [Fact]
    public async Task AcademicStructure_CreatesAndActivatesNigerianStructure()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null); var service = fixture.Academics();
        var year = await service.CreateAcademicYearAsync(Guid.NewGuid(), new("2026/2027", new(2026, 9, 1), new(2027, 7, 31)));
        var term = await service.CreateTermAsync(Guid.NewGuid(), new(year, "First Term", "T1", 1, new(2026, 9, 1), new(2026, 12, 18)));
        var stage = await service.CreateEducationStageAsync(Guid.NewGuid(), new("Junior Secondary", "JSS", 3));
        var level = await service.CreateClassLevelAsync(Guid.NewGuid(), new(stage, "JSS 1", "JSS1", 1));
        await service.ActivateAcademicYearAsync(Guid.NewGuid(), year);
        var section = await service.CreateClassSectionAsync(Guid.NewGuid(), new(level, "JSS 1 JSS1 Gold", 35));
        var subject = await service.CreateSubjectAsync(Guid.NewGuid(), new(null, "Mathematics", true));
        await service.AssignSubjectAsync(Guid.NewGuid(), new(section, subject, true));
        await service.ActivateTermAsync(Guid.NewGuid(), term);
        var result = await service.GetAsync(Guid.NewGuid(), year);
        Assert.Single(result.Terms); Assert.Single(result.ClassSections); Assert.Single(result.ClassSubjects);
        Assert.Equal("JSS 1 Gold", result.ClassSubjects.Single().ClassSectionName);
        Assert.Equal("Mathematics", result.ClassSubjects.Single().SubjectName);
        Assert.Equal("JSS 1 Gold", result.ClassSections.Single().Name);
        Assert.Equal("JSS1-GOLD", result.ClassSections.Single().Code);
        Assert.Equal(fixture.CampusA, result.ClassSections.Single().CampusId);
        Assert.Equal(year, result.ClassSections.Single().AcademicYearId);
        Assert.Equal(AcademicPeriodStatus.Active, result.AcademicYears.Single().Status);
        Assert.Equal(AcademicPeriodStatus.Active, result.Terms.Single().Status);
        await service.DeleteAsync(Guid.NewGuid(), "class-subjects", section, subject);
        Assert.Empty((await service.GetAsync(Guid.NewGuid(), year)).ClassSubjects);
    }

    [Fact]
    public async Task AcademicYears_RejectInvalidOrMismatchedSessionNames()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null); var service = fixture.Academics();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAcademicYearAsync(Guid.NewGuid(), new("Anything", new(2027, 9, 1), new(2028, 7, 31))));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAcademicYearAsync(Guid.NewGuid(), new("2027/2028", new(2027, 1, 1), new(2027, 12, 31))));
        var id = await service.CreateAcademicYearAsync(Guid.NewGuid(), new("2027/2028", new(2027, 9, 1), new(2028, 7, 31)));
        Assert.Equal("2027/2028", (await service.GetAsync(Guid.NewGuid())).AcademicYears.Single(year => year.Id == id).Name);
    }

    [Fact]
    public async Task EducationStages_UseProtectedStandardCodesAndOrdering()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null); var service = fixture.Academics();
        await service.CreateEducationStageAsync(Guid.NewGuid(), new("Junior Secondary", "RANDOM", 999));

        var stage = Assert.Single((await service.GetAsync(Guid.NewGuid())).EducationStages);
        Assert.Equal("Junior Secondary", stage.Name);
        Assert.Equal("JSS", stage.Code);
        Assert.Equal(40, stage.DisplayOrder);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateEducationStageAsync(Guid.NewGuid(), new("Random stage", "BAD", -1)));
    }

    [Fact]
    public async Task ClassLevels_UseProtectedCodesOrderingAndStageCombinations()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null); var service = fixture.Academics();
        var stageId = await service.CreateEducationStageAsync(Guid.NewGuid(), new("Senior Secondary"));
        await service.CreateClassLevelAsync(Guid.NewGuid(), new(stageId, "SS 2", "RANDOM", 999));

        var level = Assert.Single((await service.GetAsync(Guid.NewGuid())).ClassLevels);
        Assert.Equal("Senior Secondary", level.EducationStageName);
        Assert.Equal("SS 2", level.Name);
        Assert.Equal("SS2", level.Code);
        Assert.Equal(20, level.DisplayOrder);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateClassLevelAsync(Guid.NewGuid(), new(stageId, "JSS 2")));
    }

    [Fact]
    public async Task DepartmentsAndSubjects_GenerateProtectedStableCodes()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null); var service = fixture.Academics();
        var departmentId = await service.CreateDepartmentAsync(Guid.NewGuid(), new("Science & Technology", "MANIPULATED"));
        var subjectId = await service.CreateSubjectAsync(Guid.NewGuid(), new(departmentId, "Basic Science", true, "MANIPULATED"));

        var structure = await service.GetAsync(Guid.NewGuid());
        Assert.Equal("SCIENCE-TECHNOLOGY", Assert.Single(structure.Departments).Code);
        Assert.Equal("BASIC-SCIENCE", Assert.Single(structure.Subjects).Code);
        await service.UpdateAsync(Guid.NewGuid(), "departments", departmentId, new DepartmentInput("STEM", "CHANGED"));
        await service.UpdateAsync(Guid.NewGuid(), "subjects", subjectId, new SubjectInput(departmentId, "Integrated Science", false, "CHANGED"));
        structure = await service.GetAsync(Guid.NewGuid());
        Assert.Equal("SCIENCE-TECHNOLOGY", Assert.Single(structure.Departments).Code);
        Assert.Equal("BASIC-SCIENCE", Assert.Single(structure.Subjects).Code);
    }

    [Fact]
    public async Task StaffManagement_IsTenantIsolated()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        await fixture.Staff().CreateAsync(Guid.NewGuid(), new("A-001", "Ada", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, "ada.isolation@example.com", "09096735001", new(2026, 9, 1)));
        fixture.Context.Set(fixture.TenantB, null);
        var result = await fixture.Staff().ListAsync(Guid.NewGuid(), 1, 25, null, null);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task StaffExport_RejectsCrossTenantSelection()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        var staffId = await fixture.Staff().CreateAsync(Guid.NewGuid(), new(null, "Ada", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, "ada.export@example.com", "09096735001", new(2026, 9, 1)));
        fixture.Context.Set(fixture.TenantB, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Staff().ExportSelectedAsync(Guid.NewGuid(), new([staffId])));
    }

    [Fact]
    public async Task StaffExport_EscapesSpreadsheetFormulas()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        var staffId = await fixture.Staff().CreateAsync(Guid.NewGuid(), new(null, "=HYPERLINK", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, "ada.export@example.com", "09096735001", new(2026, 9, 1)));

        var csv = await fixture.Staff().ExportSelectedAsync(Guid.NewGuid(), new([staffId]));

        Assert.Contains("\"'=HYPERLINK\"", csv);
    }

    [Fact]
    public async Task StaffRoleAccess_RejectsCrossTenantStaff()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        var staffId = await fixture.Staff().CreateAsync(Guid.NewGuid(), new(null, "Ada", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, "ada.role@example.com", "09096735001", new(2026, 9, 1)));
        fixture.Context.Set(fixture.TenantB, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Staff().GetRoleAccessAsync(Guid.NewGuid(), staffId));
    }

    [Fact]
    public async Task StaffManagement_RejectsCrossTenantCampus()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantB, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Staff().CreateAsync(Guid.NewGuid(), new("B-001", "Bola", "Ade", StaffCategory.Administrative, fixture.CampusA, null, null, null, null, new(2026, 9, 1))));
    }

    [Fact]
    public async Task StaffCreation_GeneratesUniqueNumberAndValidatesNigerianPhone()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var service = fixture.Staff();
        var firstId = await service.CreateAsync(Guid.NewGuid(), new("IGNORED", "Ada", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, "ada@example.com", "09096735531", new(2026, 9, 1)));
        var secondId = await service.CreateAsync(Guid.NewGuid(), new(null, "Bola", "Ade", StaffCategory.Administrative, fixture.CampusA, null, null, "bola@example.com", "09096735532", new(2026, 9, 1)));

        var first = await service.GetAsync(Guid.NewGuid(), firstId);
        var second = await service.GetAsync(Guid.NewGuid(), secondId);
        Assert.StartsWith("STF-2026-", first.StaffNumber);
        Assert.NotEqual(first.StaffNumber, second.StaffNumber);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Guid.NewGuid(), new(null, "Bad", "Phone", StaffCategory.NonTeaching, fixture.CampusA, null, null, "bad@example.com", "+2348096735531", new(2026, 9, 1))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Guid.NewGuid(), new(null, "Duplicate", "Email", StaffCategory.NonTeaching, fixture.CampusA, null, null, "ADA@EXAMPLE.COM", "09096735533", new(2026, 9, 1))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Guid.NewGuid(), new(null, "Duplicate", "Phone", StaffCategory.NonTeaching, fixture.CampusA, null, null, "unique@example.com", "09096735531", new(2026, 9, 1))));
    }

    [Fact]
    public async Task Position_CreatesProtectedCodeAndRejectsDuplicateName()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        var service = fixture.Staff();

        var positionId = await service.CreatePositionAsync(Guid.NewGuid(), new("Head Teacher"));

        var position = Assert.Single(await service.ListPositionsAsync(Guid.NewGuid()), item => item.IsCustom);
        Assert.Equal("Head Teacher", position.Name);
        Assert.Equal("HEAD-TEACHER", position.Code);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePositionAsync(Guid.NewGuid(), new("head teacher")));
        await service.UpdatePositionAsync(Guid.NewGuid(), positionId, new("Senior Head Teacher"));
        Assert.Equal("Senior Head Teacher", Assert.Single(await service.ListPositionsAsync(Guid.NewGuid()), item => item.IsCustom).Name);
        await service.DeletePositionAsync(Guid.NewGuid(), positionId);
        Assert.DoesNotContain(await service.ListPositionsAsync(Guid.NewGuid()), item => item.IsCustom);
    }

    [Fact]
    public async Task Position_DeleteIsBlockedWhileAssignedToStaff()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        var service = fixture.Staff();
        var positionId = await service.CreatePositionAsync(Guid.NewGuid(), new("Class Teacher", StaffCategory.Teaching));
        await service.CreateAsync(Guid.NewGuid(), new("A-002", "Tola", "Akin", StaffCategory.Teaching, fixture.CampusA, null, positionId, "tola@example.com", "09096735002", new(2026, 9, 1)));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePositionAsync(Guid.NewGuid(), positionId));
        Assert.Contains("assigned to staff", error.Message);
    }

    [Fact]
    public async Task AdditionalPositions_AreTenantScopedAndCannotBeDuplicated()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        var service = fixture.Staff();
        var primary = await service.CreatePositionAsync(Guid.NewGuid(), new("ICT Specialist", StaffCategory.Administrative));
        var teaching = await service.CreatePositionAsync(Guid.NewGuid(), new("Computer Instructor", StaffCategory.Teaching));
        var staffId = await service.CreateAsync(Guid.NewGuid(), new(null, "Ada", "Okafor", StaffCategory.Administrative, fixture.CampusA, null, primary, "ada.positions@example.com", "09096735003", new(2026, 9, 1)));

        await service.AddStaffPositionAsync(Guid.NewGuid(), staffId, teaching);
        Assert.Equal(2, (await service.ListStaffPositionsAsync(Guid.NewGuid(), staffId)).Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddStaffPositionAsync(Guid.NewGuid(), staffId, teaching));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePositionAsync(Guid.NewGuid(), teaching));
        await service.SetPrimaryStaffPositionAsync(Guid.NewGuid(), staffId, teaching);
        Assert.Equal(StaffCategory.Teaching, (await service.GetAsync(Guid.NewGuid(), staffId)).Category);
        var positions = await service.ListStaffPositionsAsync(Guid.NewGuid(), staffId);
        Assert.Contains(positions, item => item.PositionId == teaching && item.IsPrimary);
        Assert.Contains(positions, item => item.PositionId == primary && !item.IsPrimary);

        fixture.Context.Set(fixture.TenantB, null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ListStaffPositionsAsync(Guid.NewGuid(), staffId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AddStaffPositionAsync(Guid.NewGuid(), staffId, teaching));
    }

    [Fact]
    public async Task AdministrativeStaff_NeedsTeachingPositionForTeachingAssignment()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var staffService = fixture.Staff();
        var primary = await staffService.CreatePositionAsync(Guid.NewGuid(), new("ICT Specialist", StaffCategory.Administrative));
        var teaching = await staffService.CreatePositionAsync(Guid.NewGuid(), new("Computer Instructor", StaffCategory.Teaching));
        var staffId = await staffService.CreateAsync(Guid.NewGuid(), new(null, "Ada", "Okafor", StaffCategory.Administrative, fixture.CampusA, null, primary, "ada.teacher@example.com", "09096735004", new(2026, 9, 1)));
        var academics = fixture.Academics();
        var yearId = await academics.CreateAcademicYearAsync(Guid.NewGuid(), new("2026/2027", new(2026, 9, 1), new(2027, 7, 31)));
        await academics.ActivateAcademicYearAsync(Guid.NewGuid(), yearId);
        var stageId = await academics.CreateEducationStageAsync(Guid.NewGuid(), new("Junior Secondary", "JSS", 1));
        var levelId = await academics.CreateClassLevelAsync(Guid.NewGuid(), new(stageId, "JSS 1", "JSS1", 1));
        var sectionId = await academics.CreateClassSectionAsync(Guid.NewGuid(), new(levelId, "A", 30));

        await Assert.ThrowsAsync<InvalidOperationException>(() => staffService.CreateTeachingAssignmentAsync(Guid.NewGuid(), new(staffId, sectionId, null, TeachingAssignmentRole.ClassTeacher)));
        await staffService.AddStaffPositionAsync(Guid.NewGuid(), staffId, teaching);
        await staffService.CreateTeachingAssignmentAsync(Guid.NewGuid(), new(staffId, sectionId, null, TeachingAssignmentRole.ClassTeacher));
        var duplicate = await Assert.ThrowsAsync<TeachingAssignmentConflictException>(() =>
            staffService.CreateTeachingAssignmentAsync(Guid.NewGuid(), new(staffId, sectionId, null, TeachingAssignmentRole.ClassTeacher)));
        Assert.Contains("already has", duplicate.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => staffService.RemoveStaffPositionAsync(Guid.NewGuid(), staffId, teaching));
        Assert.Single(await staffService.ListTeachingAssignmentsAsync(Guid.NewGuid(), sectionId));
    }

    [Fact]
    public async Task StaffView_IsRestrictedToTheActorsLinkedProfileWithoutManagePermission()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var actor = Guid.NewGuid(); var ownId = Guid.NewGuid(); var otherId = Guid.NewGuid();
        var own = new StaffProfile(ownId, fixture.TenantA, "OWN-1", "Own", "Staff", StaffCategory.Teaching, fixture.CampusA, null, null, null, null, new(2026, 9, 1), fixture.Clock.UtcNow);
        own.LinkUser(actor, fixture.Clock.UtcNow);
        fixture.Db.StaffProfiles.AddRange(own, new StaffProfile(otherId, fixture.TenantA, "OTHER-1", "Other", "Staff", StaffCategory.Administrative, fixture.CampusA, null, null, null, null, new(2026, 9, 1), fixture.Clock.UtcNow));
        await fixture.Db.SaveChangesAsync();

        var service = fixture.Staff(new ViewOnlyPermissions());
        var result = await service.ListAsync(actor, 1, 25, null, null);

        Assert.Equal(ownId, Assert.Single(result.Items).Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(actor, otherId));
    }

    [Fact]
    public async Task Applicants_AreTenantIsolated()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        await fixture.Students().CreateApplicantAsync(Guid.NewGuid(), new("Ngozi", "Ibe", new(2015, 1, 1), null, null, null, null, ApplicationNumber: "APP-A",
            Guardians: [new("Guardian One", GuardianRelationshipType.Parent, "08011112222", "guardian@example.test", "1 Test Street")]));
        fixture.Context.Set(fixture.TenantB, null);
        var result = await fixture.Students().ListApplicantsAsync(Guid.NewGuid(), 1, 25, null);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ApplicantGuardians_AreTenantIsolatedAndRequiredOnCreation()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var actor = Guid.NewGuid();
        var service = fixture.Students();

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateApplicantAsync(actor,
            new("Chika", "Eze", new(2015, 6, 1), null, null, null, null)));

        var applicantId = await service.CreateApplicantAsync(actor, new("Chika", "Eze", new(2015, 6, 1), null, null, null, null,
            Guardians: [new("Amara Eze", GuardianRelationshipType.Mother, "08033445566", "amara.eze@example.test", "12 Palm Avenue", IsPrimary: true)]));
        Assert.True(await fixture.Db.ApplicantGuardians.AnyAsync(x => x.ApplicantId == applicantId && x.Name == "Amara Eze"));

        fixture.Context.Set(fixture.TenantB, null);
        Assert.False(await fixture.Db.ApplicantGuardians.AnyAsync(x => x.ApplicantId == applicantId));
        var crossTenantResult = await fixture.Students().ListApplicantsAsync(Guid.NewGuid(), 1, 25, null);
        Assert.Empty(crossTenantResult.Items);
    }

    [Fact]
    public async Task ImportOperations_AreTenantIsolated()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var applicantImportId = Guid.NewGuid(); var studentImportId = Guid.NewGuid(); var guardianImportId = Guid.NewGuid();
        fixture.Db.ImportOperations.AddRange(
            new ImportOperation(applicantImportId, fixture.TenantA, "Applicants", Guid.NewGuid(), fixture.Clock.UtcNow),
            new ImportOperation(studentImportId, fixture.TenantA, "Students", Guid.NewGuid(), fixture.Clock.UtcNow),
            new ImportOperation(guardianImportId, fixture.TenantA, "Guardians", Guid.NewGuid(), fixture.Clock.UtcNow, fixture.CampusA));
        await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear(); fixture.Context.Set(fixture.TenantB, fixture.CampusA);
        Assert.Empty(await fixture.Db.ImportOperations.ToListAsync());
        var applicants = new ApplicantImportService(fixture.Db, fixture.Context, new AllowedAccess(), null!, fixture.Clock, null!);
        var profiles = new ProfileImportService(fixture.Db, fixture.Context, new AllowedAccess(), null!, fixture.Clock, null!, null!, new AllowedPermissions());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => applicants.GetAsync(Guid.NewGuid(), applicantImportId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => profiles.GetStudentsAsync(Guid.NewGuid(), studentImportId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => profiles.GetGuardiansAsync(Guid.NewGuid(), guardianImportId));
    }

    [Fact]
    public async Task Exports_RequireManageAccess_AreTenantIsolated_AndExcludeSensitiveFields()
    {
        await using var fixture = await Fixture.CreateAsync(); var now = fixture.Clock.UtcNow;
        fixture.Context.Set(fixture.TenantA, null);
        var applicantId = Guid.NewGuid(); var studentId = Guid.NewGuid();
        fixture.Db.Applicants.Add(new Applicant(applicantId, fixture.TenantA, "APP-A", "Ada", "Applicant", new(2015, 1, 1), "ada@example.test", "0801", null, null, now));
        fixture.Db.Students.Add(new Student(studentId, fixture.TenantA, "STU-A", "Sam", "Student", new(2014, 1, 1), null, now, "sam@example.test"));
        fixture.Db.Guardians.Add(new Guardian(Guid.NewGuid(), fixture.TenantA, "Grace", "Guardian", "0802", "grace@example.test", now));
        fixture.Db.ApplicantSensitiveRecords.Add(new ApplicantSensitiveRecord(fixture.TenantA, applicantId, "SECRET-APPLICANT-ADDRESS", "SECRET-MEDICAL", "SECRET-ALLERGY", "SECRET-SEN", now));
        fixture.Db.StudentSensitiveRecords.Add(new StudentSensitiveRecord(fixture.TenantA, studentId, "SECRET-STUDENT-ADDRESS", "SECRET-STUDENT-MEDICAL", null, null, null, null, null, null, null, "SECRET-PRIVATE-NOTE", now));
        await fixture.Db.SaveChangesAsync();
        fixture.Context.Set(fixture.TenantB, null);
        fixture.Db.Students.Add(new Student(Guid.NewGuid(), fixture.TenantB, "STU-B", "Other", "Tenant", new(2014, 1, 1), null, now)); await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear(); fixture.Context.Set(fixture.TenantA, null);

        var exports = new DataPortabilityService(fixture.Db, new AllowedAccess());
        var applicantCsv = await exports.ExportApplicantsAsync(Guid.NewGuid()); var studentCsv = await exports.ExportStudentsAsync(Guid.NewGuid()); var guardianCsv = await exports.ExportGuardiansAsync(Guid.NewGuid());

        Assert.Contains("APP-A", applicantCsv); Assert.Contains("STU-A", studentCsv); Assert.Contains("Grace", guardianCsv); Assert.DoesNotContain("STU-B", studentCsv);
        var combined = applicantCsv + studentCsv + guardianCsv;
        Assert.DoesNotContain("SECRET-", combined, StringComparison.Ordinal);
        var denied = new DataPortabilityService(fixture.Db, new DeniedAccess());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => denied.ExportApplicantsAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => denied.ExportStudentsAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => denied.ExportGuardiansAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GuardianAccess_IsRestrictedToLinkedChildrenAndOwnProfile()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var guardianUser = Guid.NewGuid(); var ownGuardianId = Guid.NewGuid(); var otherGuardianId = Guid.NewGuid(); var linkedStudentId = Guid.NewGuid(); var otherStudentId = Guid.NewGuid();
        var ownGuardian = new Guardian(ownGuardianId, fixture.TenantA, "Ada", "Parent", "0801", "ada@example.test", fixture.Clock.UtcNow); ownGuardian.LinkUser(guardianUser);
        fixture.Db.Guardians.AddRange(ownGuardian, new Guardian(otherGuardianId, fixture.TenantA, "Other", "Parent", "0802", null, fixture.Clock.UtcNow));
        fixture.Db.Students.AddRange(new Student(linkedStudentId, fixture.TenantA, "A-1", "Linked", "Child", new(2015, 1, 1), null, fixture.Clock.UtcNow), new Student(otherStudentId, fixture.TenantA, "A-2", "Other", "Child", new(2015, 1, 1), null, fixture.Clock.UtcNow));
        fixture.Db.StudentGuardians.Add(new StudentGuardian(fixture.TenantA, linkedStudentId, ownGuardianId, GuardianRelationshipType.Parent, true, true, true)); await fixture.Db.SaveChangesAsync();

        var service = fixture.Students(new ViewOnlyPermissions());
        var students = await service.ListStudentsAsync(guardianUser, 1, 25, null); var guardians = await service.ListGuardiansAsync(guardianUser, 1, 25, null);

        Assert.Equal(linkedStudentId, Assert.Single(students.Items).Id); Assert.Equal(ownGuardianId, Assert.Single(guardians.Items).Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetStudentAsync(guardianUser, otherStudentId));
    }

    [Fact]
    public async Task StudentAccess_IsRestrictedToOwnCanonicalRecord()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var userId = Guid.NewGuid(); var ownId = Guid.NewGuid(); var otherId = Guid.NewGuid();
        var own = new Student(ownId, fixture.TenantA, "OWN-1", "Own", "Learner", new(2015, 1, 1), null, fixture.Clock.UtcNow, "student@example.test"); own.LinkUser(userId);
        fixture.Db.Students.AddRange(own, new Student(otherId, fixture.TenantA, "OTHER-1", "Other", "Learner", new(2015, 1, 1), null, fixture.Clock.UtcNow)); await fixture.Db.SaveChangesAsync();

        var service = fixture.Students(new ViewOnlyPermissions());
        var students = await service.ListStudentsAsync(userId, 1, 25, null);

        Assert.Equal(ownId, Assert.Single(students.Items).Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetStudentAsync(userId, otherId));
    }

    [Fact]
    public async Task SensitiveStudentDataAndDocuments_RespectLinkedResourceScope()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var actor = Guid.NewGuid(); var ownId = Guid.NewGuid(); var otherId = Guid.NewGuid();
        var own = new Student(ownId, fixture.TenantA, "OWN-S", "Own", "Student", new(2015, 1, 1), null, fixture.Clock.UtcNow); own.LinkUser(actor);
        fixture.Db.Students.AddRange(own, new Student(otherId, fixture.TenantA, "OTHER-S", "Other", "Student", new(2015, 1, 1), null, fixture.Clock.UtcNow));
        fixture.Db.StudentSensitiveRecords.AddRange(new StudentSensitiveRecord(fixture.TenantA, ownId, "Own address", null, null, null, null, null, null, null, null, null, fixture.Clock.UtcNow), new StudentSensitiveRecord(fixture.TenantA, otherId, "Other address", null, null, null, null, null, null, null, null, null, fixture.Clock.UtcNow)); await fixture.Db.SaveChangesAsync();
        var permissions = new ViewOnlyPermissions(); var students = fixture.Students(permissions);
        var documents = new PhaseOneDocumentService(fixture.Db, new AllowedAccess(), permissions, new UnusedFileService());

        Assert.Equal("Own address", (await students.GetStudentSensitiveAsync(actor, ownId))?.Address);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => students.GetStudentSensitiveAsync(actor, otherId));
        Assert.Empty(await documents.ListAsync(actor, "Student", ownId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => documents.ListAsync(actor, "Student", otherId));
    }

    [Fact]
    public async Task GuardianCreation_RequiresContactAndRejectsStudentOutsideActiveCampus()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var actor = Guid.NewGuid();
        var service = fixture.Students();

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateGuardianAsync(actor,
            new GuardianInput("Ada", "Parent", "09096735531", null, "School Road")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateGuardianAsync(actor,
            new GuardianInput("Ada", "Parent", "09096735531", "ada@example.test", "")));

        var otherTenantStudent = Guid.NewGuid();
        fixture.Context.Set(fixture.TenantB, null);
        fixture.Db.Students.Add(new Student(otherTenantStudent, fixture.TenantB, "B-001", "Other", "Child", new(2015, 1, 1), null, fixture.Clock.UtcNow));
        await fixture.Db.SaveChangesAsync();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateGuardianAsync(actor,
            new GuardianInput("Ada", "Parent", "09096735531", "ada@example.test", "School Road", otherTenantStudent, GuardianRelationshipType.Mother)));
        Assert.Empty(await fixture.Db.Guardians.ToListAsync());
        Assert.Empty(await service.SearchGuardianStudentsAsync(actor, "Other"));
    }

    [Fact]
    public async Task GuardianCreation_LinksMultipleStudentsWithinTheActiveCampus()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var yearId = Guid.NewGuid(); var levelId = Guid.NewGuid(); var sectionId = Guid.NewGuid(); var now = fixture.Clock.UtcNow;
        var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid();
        fixture.Db.ClassSections.Add(new ClassSection(sectionId, fixture.TenantA, fixture.CampusA, yearId, levelId, "Primary 3 A", "PRI3-A", 30, now));
        fixture.Db.Students.AddRange(
            new Student(firstId, fixture.TenantA, "STU-001", "First", "Child", new(2017, 1, 1), null, now),
            new Student(secondId, fixture.TenantA, "STU-002", "Second", "Child", new(2018, 1, 1), null, now));
        fixture.Db.Enrollments.AddRange(
            new Enrollment(Guid.NewGuid(), fixture.TenantA, firstId, yearId, sectionId, new(2026, 9, 1), now),
            new Enrollment(Guid.NewGuid(), fixture.TenantA, secondId, yearId, sectionId, new(2026, 9, 1), now));
        await fixture.Db.SaveChangesAsync();

        var guardianId = await fixture.Students().CreateGuardianAsync(Guid.NewGuid(), new GuardianInput(
            "Ada", "Parent", "09096735531", "ada@example.test", "School Road", StudentLinks:
            [new(firstId, GuardianRelationshipType.Mother, true, true, true),
             new(secondId, GuardianRelationshipType.Mother, true, true, true)]));

        var links = await fixture.Db.StudentGuardians.Where(x => x.GuardianId == guardianId).ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.Contains(links, x => x.StudentId == firstId);
        Assert.Contains(links, x => x.StudentId == secondId);
    }

    [Fact]
    public async Task StudentRegistration_GeneratesNumber_EnrolsInActiveCampus_AndReusesGuardian()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var now = fixture.Clock.UtcNow; var yearId = Guid.NewGuid(); var sectionId = Guid.NewGuid(); var levelId = Guid.NewGuid();
        fixture.Db.AcademicYears.Add(new AcademicYear(yearId, fixture.TenantA, "2026/2027", new(2026, 9, 1), new(2027, 7, 31), now));
        fixture.Db.ClassSections.Add(new ClassSection(sectionId, fixture.TenantA, fixture.CampusA, yearId, levelId, "JSS 1 A", "JSS1-A", 30, now));
        var existingGuardian = new Guardian(Guid.NewGuid(), fixture.TenantA, "Ada", "Parent", "09096735531", "ada@example.test", now);
        fixture.Db.Guardians.Add(existingGuardian); await fixture.Db.SaveChangesAsync();

        var registration = new StudentRegistrationInput(
            "Tomi", "K", "Student", new DateOnly(2014, 2, 3), "Female", StudentType.Day, null, null,
            yearId, sectionId, new DateOnly(2026, 9, 7), "School Road", null, null, null, null,
            [new StudentGuardianRegistrationInput("Ada", "Parent", "09096735531", "ada@example.test", "Female",
                "School Road", GuardianRelationshipType.Mother)]);
        var result = await fixture.Students().CreateStudentAsync(Guid.NewGuid(), registration);

        Assert.StartsWith("STU-", result.AdmissionNumber);
        var guardianResult = Assert.Single(result.Guardians);
        Assert.Equal(existingGuardian.Id, guardianResult.GuardianId);
        Assert.False(guardianResult.NeedsInvitation);
        Assert.Equal(1, await fixture.Db.Guardians.CountAsync());
        Assert.True(await fixture.Db.Enrollments.AnyAsync(x => x.StudentId == result.StudentId && x.ClassSectionId == sectionId));
        Assert.True(await fixture.Db.StudentGuardians.AnyAsync(x => x.StudentId == result.StudentId && x.GuardianId == existingGuardian.Id));
        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Students().CreateStudentAsync(Guid.NewGuid(), registration));
        Assert.Contains("same name, date of birth and gender", duplicate.Message);
    }

    [Fact]
    public async Task GuardianDocuments_RespectOwnProfileScope()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var actor = Guid.NewGuid();
        var own = new Guardian(Guid.NewGuid(), fixture.TenantA, "Ada", "Parent", "09096735531", "ada@example.test", fixture.Clock.UtcNow);
        own.LinkUser(actor);
        var other = new Guardian(Guid.NewGuid(), fixture.TenantA, "Bola", "Parent", "09096735532", "bola@example.test", fixture.Clock.UtcNow);
        fixture.Db.Guardians.AddRange(own, other);
        await fixture.Db.SaveChangesAsync();
        var documents = new PhaseOneDocumentService(fixture.Db, new AllowedAccess(), new ViewOnlyPermissions(), new UnusedFileService());

        Assert.Empty(await documents.ListAsync(actor, "Guardian", own.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => documents.ListAsync(actor, "Guardian", other.Id));
    }

    [Fact]
    public async Task GuardianBin_HidesActiveRecord_AndRestoresWithinItsTenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var guardian = new Guardian(Guid.NewGuid(), fixture.TenantA, "Ada", "Parent", "09096735531", "ada@example.test", fixture.Clock.UtcNow);
        fixture.Db.Guardians.Add(guardian);
        await fixture.Db.SaveChangesAsync();
        var actor = Guid.NewGuid();
        var bin = new GuardianBinService(fixture.Db, fixture.Context, new AllowedAccess(), new AllowedPermissions(),
            new TestFileObjectStorage(), fixture.Clock);

        await bin.MoveAsync(actor, guardian.Id);
        Assert.Empty(await fixture.Db.Guardians.ToListAsync());
        Assert.Equal(1, await bin.CountAsync(actor));
        Assert.Equal(guardian.Id, Assert.Single((await bin.ListAsync(actor, 1, 20)).Items).Id);
        Assert.Empty((await bin.ListAsync(actor, 1, 20, "nobody")).Items);

        fixture.Context.Set(fixture.TenantB, fixture.CampusA);
        Assert.Equal(0, await bin.CountAsync(actor));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => bin.RestoreAsync(actor, guardian.Id));

        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        await bin.RestoreAsync(actor, guardian.Id);
        Assert.Equal(guardian.Id, Assert.Single(await fixture.Db.Guardians.ToListAsync()).Id);
        Assert.Equal(0, await bin.CountAsync(actor));
    }

    [Fact]
    public async Task GuardianBin_RequiresExplicitStudentUnlinkConfirmation()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var guardian = new Guardian(Guid.NewGuid(), fixture.TenantA, "Ada", "Parent", "09096735531", "ada@example.test", fixture.Clock.UtcNow);
        var student = new Student(Guid.NewGuid(), fixture.TenantA, "STU-LINK", "Linked", "Child", new(2016, 1, 1), null, fixture.Clock.UtcNow);
        var secondStudent = new Student(Guid.NewGuid(), fixture.TenantA, "STU-LINK-2", "Second", "Child", new(2017, 1, 1), null, fixture.Clock.UtcNow);
        fixture.Db.Guardians.Add(guardian); fixture.Db.Students.AddRange(student, secondStudent);
        fixture.Db.StudentGuardians.AddRange(
            new StudentGuardian(fixture.TenantA, student.Id, guardian.Id, GuardianRelationshipType.Mother, true, true, true),
            new StudentGuardian(fixture.TenantA, secondStudent.Id, guardian.Id, GuardianRelationshipType.Mother, false, false, true));
        await fixture.Db.SaveChangesAsync();
        var actor = Guid.NewGuid(); var bin = new GuardianBinService(fixture.Db, fixture.Context, new AllowedAccess(), new AllowedPermissions(), new TestFileObjectStorage(), fixture.Clock);

        var blocked = await Assert.ThrowsAsync<GuardianStudentLinksExistException>(() => bin.MoveAsync(actor, guardian.Id));
        Assert.Contains("linked to 2 students", blocked.Message);
        Assert.Equal(2, await fixture.Db.StudentGuardians.CountAsync());
        var links = await bin.ListStudentLinksAsync(actor, guardian.Id);
        Assert.Contains(links, link => link.StudentId == student.Id && link.FirstName == "Linked" && link.AdmissionNumber == "STU-LINK");

        fixture.Context.Set(fixture.TenantB, fixture.CampusA);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => bin.ListStudentLinksAsync(actor, guardian.Id));
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);

        await bin.UnlinkStudentAsync(actor, guardian.Id, student.Id);
        Assert.Single(await fixture.Db.StudentGuardians.ToListAsync());
        await bin.UnlinkAllStudentsAsync(actor, guardian.Id);
        Assert.Empty(await fixture.Db.StudentGuardians.ToListAsync());
        await bin.MoveAsync(actor, guardian.Id);
        Assert.Equal(1, await bin.CountAsync(actor));
    }

    [Fact]
    public async Task SelectedGuardianExport_RejectsCrossTenantAndBinnedIds()
    {
        await using var fixture = await Fixture.CreateAsync();
        var actor = Guid.NewGuid();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var active = new Guardian(Guid.NewGuid(), fixture.TenantA, "Ada", "Parent", "09096735531", "ada@example.test", fixture.Clock.UtcNow);
        var binned = new Guardian(Guid.NewGuid(), fixture.TenantA, "Bola", "Parent", "09096735532", "bola@example.test", fixture.Clock.UtcNow);
        fixture.Db.Guardians.AddRange(active, binned);
        await fixture.Db.SaveChangesAsync();
        var bin = new GuardianBinService(fixture.Db, fixture.Context, new AllowedAccess(), new AllowedPermissions(), new TestFileObjectStorage(), fixture.Clock);
        await bin.MoveAsync(actor, binned.Id);
        fixture.Context.Set(fixture.TenantB, null);
        var other = new Guardian(Guid.NewGuid(), fixture.TenantB, "Other", "Parent", "09096735533", "other@example.test", fixture.Clock.UtcNow);
        fixture.Db.Guardians.Add(other);
        await fixture.Db.SaveChangesAsync();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var exports = new DataPortabilityService(fixture.Db, new AllowedAccess());

        Assert.Contains("ada@example.test", await exports.ExportSelectedGuardiansAsync(actor, [active.Id]));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => exports.ExportSelectedGuardiansAsync(actor, [active.Id, other.Id]));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => exports.ExportSelectedGuardiansAsync(actor, [binned.Id]));
        await Assert.ThrowsAsync<ArgumentException>(() => exports.ExportSelectedGuardiansAsync(actor, [active.Id, active.Id]));
    }

    [Fact]
    public async Task GuardianDirectory_SearchSortAndPagingRemainTenantScoped()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        fixture.Db.Guardians.AddRange(
            new Guardian(Guid.NewGuid(), fixture.TenantA, "Ada", "Zulu", "09096735531", "ada@example.test", fixture.Clock.UtcNow),
            new Guardian(Guid.NewGuid(), fixture.TenantA, "Bola", "Alpha", "09096735532", "bola@example.test", fixture.Clock.UtcNow));
        await fixture.Db.SaveChangesAsync();
        fixture.Context.Set(fixture.TenantB, null);
        fixture.Db.Guardians.Add(new Guardian(Guid.NewGuid(), fixture.TenantB, "Other", "Alpha", "09096735533", "other@example.test", fixture.Clock.UtcNow));
        await fixture.Db.SaveChangesAsync();
        fixture.Context.Set(fixture.TenantA, null);

        var service = fixture.Students();
        var first = await service.ListGuardiansAsync(Guid.NewGuid(), 1, 1, null, sort: "lastName");
        var second = await service.ListGuardiansAsync(Guid.NewGuid(), 2, 1, null, sort: "lastName");
        var match = await service.ListGuardiansAsync(Guid.NewGuid(), 1, 20, "ADA@EXAMPLE", sort: "email");

        Assert.Equal(2, first.Total);
        Assert.Equal("Alpha", Assert.Single(first.Items).LastName);
        Assert.Equal("Zulu", Assert.Single(second.Items).LastName);
        Assert.Equal("Ada", Assert.Single(match.Items).FirstName);
    }

    [Fact]
    public async Task GuardianDirectory_ClassFiltersUseActiveTenantScopedEnrollments()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, fixture.CampusA);
        var now = fixture.Clock.UtcNow; var stageId = Guid.NewGuid(); var levelId = Guid.NewGuid(); var otherLevelId = Guid.NewGuid(); var yearId = Guid.NewGuid(); var sectionId = Guid.NewGuid();
        var guardian = new Guardian(Guid.NewGuid(), fixture.TenantA, "Ada", "Parent", "09096735531", "ada@example.test", now);
        var otherGuardian = new Guardian(Guid.NewGuid(), fixture.TenantA, "Bola", "Parent", "09096735532", "bola@example.test", now);
        var student = new Student(Guid.NewGuid(), fixture.TenantA, "STU-FILTER", "Linked", "Student", new(2015, 1, 1), null, now);
        fixture.Db.EducationStages.Add(new EducationStage(stageId, fixture.TenantA, "Primary", "PRI", 10, now));
        fixture.Db.ClassLevels.AddRange(new ClassLevel(levelId, fixture.TenantA, stageId, "Primary 1", "PRI1", 10, now), new ClassLevel(otherLevelId, fixture.TenantA, stageId, "Primary 2", "PRI2", 20, now));
        fixture.Db.AcademicYears.Add(new AcademicYear(yearId, fixture.TenantA, "2026/2027", new(2026, 9, 1), new(2027, 7, 31), now));
        fixture.Db.ClassSections.Add(new ClassSection(sectionId, fixture.TenantA, fixture.CampusA, yearId, levelId, "Primary 1 A", "PRI1-A", 30, now));
        fixture.Db.Guardians.AddRange(guardian, otherGuardian); fixture.Db.Students.Add(student);
        fixture.Db.StudentGuardians.Add(new StudentGuardian(fixture.TenantA, student.Id, guardian.Id, GuardianRelationshipType.Mother, true, true, true));
        fixture.Db.Enrollments.Add(new Enrollment(Guid.NewGuid(), fixture.TenantA, student.Id, yearId, sectionId, new(2026, 9, 1), now));
        await fixture.Db.SaveChangesAsync();

        var service = fixture.Students();
        var byLevel = await service.ListGuardiansAsync(Guid.NewGuid(), 1, 20, null, classLevelId: levelId);
        var bySection = await service.ListGuardiansAsync(Guid.NewGuid(), 1, 20, null, classSectionId: sectionId);
        var otherLevel = await service.ListGuardiansAsync(Guid.NewGuid(), 1, 20, null, classLevelId: otherLevelId);

        Assert.Equal(guardian.Id, Assert.Single(byLevel.Items).Id);
        Assert.Equal(guardian.Id, Assert.Single(bySection.Items).Id);
        Assert.Empty(otherLevel.Items);
    }

    [Fact]
    public async Task StaffBin_IsTenantScoped_AndRestoreReturnsRecordToActiveDirectory()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var actor = Guid.NewGuid(); var staffId = Guid.NewGuid();
        fixture.Db.Users.Add(new PlatformUser { Id = actor, UserName = "bin-admin@example.test", DisplayName = "Ada Admin", CreatedAtUtc = fixture.Clock.UtcNow });
        fixture.Db.StaffProfiles.Add(new StaffProfile(staffId, fixture.TenantA, "BIN-1", "Ada", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, null, null, new(2026, 9, 1), fixture.Clock.UtcNow));
        await fixture.Db.SaveChangesAsync();
        await fixture.Staff().MoveToBinAsync(actor, staffId);
        fixture.Db.ChangeTracker.Clear();

        Assert.Empty((await fixture.Staff().ListAsync(actor, 1, 25, null, null)).Items);
        Assert.Equal(staffId, Assert.Single((await fixture.Staff().ListBinAsync(actor, 1, 25)).Items).Id);
        var history = await fixture.Staff().GetBinDetailAsync(actor, staffId);
        Assert.Equal(actor, history.DeletedByUserId);
        Assert.Equal("Ada Admin", history.DeletedByName);
        Assert.Contains(history.Activity, item => item.Action == "Staff.MoveToBin" && item.ActorUserId == actor);
        Assert.Contains(history.Activity, item => item.Action == "Staff.MoveToBin" && item.ActorName == "Ada Admin");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Staff().GetAsync(actor, staffId));
        fixture.Context.Set(fixture.TenantB, null);
        Assert.Equal(0, await fixture.Staff().CountBinAsync(actor));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Staff().GetBinDetailAsync(actor, staffId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Staff().RestoreAsync(actor, staffId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Staff().PermanentlyDeleteAsync(actor, staffId));
        fixture.Context.Set(fixture.TenantA, null);
        await fixture.Staff().RestoreAsync(actor, staffId);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(staffId, Assert.Single((await fixture.Staff().ListAsync(actor, 1, 25, null, null)).Items).Id);
        Assert.Equal(0, await fixture.Staff().CountBinAsync(actor));
    }

    [Fact]
    public async Task StaffPermanentDelete_RequiresBin_AndKeepsAuditHistory()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var actor = Guid.NewGuid(); var staffId = Guid.NewGuid();
        fixture.Db.StaffProfiles.Add(new StaffProfile(staffId, fixture.TenantA, "PURGE-1", "Bola", "Eze", StaffCategory.Administrative, fixture.CampusA, null, null, null, null, new(2026, 9, 1), fixture.Clock.UtcNow));
        fixture.Db.StoredFiles.Add(new StoredFile(Guid.NewGuid(), fixture.TenantA, "staff/purge.png", "purge.png", "image/png", 3, "photo", "StaffProfile", staffId, actor, fixture.Clock.UtcNow));
        var definitionId = Guid.NewGuid();
        fixture.Db.CustomFieldDefinitions.Add(new CustomFieldDefinition(definitionId, fixture.TenantA, "Hr", "StaffProfile", "test_field", "Test field", CustomFieldDataType.ShortText, fixture.Clock.UtcNow));
        fixture.Db.CustomFieldValues.Add(new CustomFieldValue(Guid.NewGuid(), fixture.TenantA, definitionId, "StaffProfile", staffId, "\"private\"", fixture.Clock.UtcNow));
        fixture.Db.AccountInvitations.Add(new AccountInvitation(Guid.NewGuid(), fixture.TenantA, InvitationTargetType.Staff, staffId, "bola@example.test", new string('B', 64), fixture.Clock.UtcNow, fixture.Clock.UtcNow.AddDays(1)));
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Staff().PermanentlyDeleteAsync(actor, staffId));
        await fixture.Staff().MoveToBinAsync(actor, staffId);
        Assert.NotNull(await fixture.Db.AccountInvitations.Where(x => x.TargetId == staffId).Select(x => x.RevokedAtUtc).SingleAsync());
        await fixture.Staff().PermanentlyDeleteAsync(actor, staffId);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(0, await fixture.Staff().CountBinAsync(actor));
        Assert.False(await fixture.Db.StaffProfiles.IgnoreQueryFilters().AnyAsync(x => x.Id == staffId));
        Assert.False(await fixture.Db.StoredFiles.AnyAsync(x => x.EntityId == staffId && x.EntityType == "StaffProfile"));
        Assert.False(await fixture.Db.CustomFieldValues.AnyAsync(x => x.EntityId == staffId && x.EntityType == "StaffProfile"));
        Assert.False(await fixture.Db.AccountInvitations.AnyAsync(x => x.TargetId == staffId && x.TargetType == InvitationTargetType.Staff));
        Assert.Equal(2, await fixture.Db.AuditRecords.CountAsync(x => x.TargetId == staffId.ToString()));
    }

    [Fact]
    public async Task StaffBin_RemovesStaffPermission_ButPreservesLinkedGuardianAccess()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var actor = Guid.NewGuid(); var person = Guid.NewGuid(); var staffId = Guid.NewGuid();
        var membership = new TenantMembership(Guid.NewGuid(), fixture.TenantA, person, fixture.Clock.UtcNow);
        var staffRole = new TenantRole(Guid.NewGuid(), fixture.TenantA, "Teacher");
        var parentRole = new TenantRole(Guid.NewGuid(), fixture.TenantA, "Parent");
        var staffPermission = new Permission(Guid.NewGuid(), Permissions.StaffView, "View staff");
        var parentPermission = new Permission(Guid.NewGuid(), Permissions.GuardiansView, "View guardians");
        var staff = new StaffProfile(staffId, fixture.TenantA, "DUAL-1", "Ada", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, null, null, new(2026, 9, 1), fixture.Clock.UtcNow);
        staff.LinkUser(person, fixture.Clock.UtcNow);
        var guardian = new Guardian(Guid.NewGuid(), fixture.TenantA, "Ada", "Okafor", "08012345678", null, fixture.Clock.UtcNow);
        guardian.LinkUser(person);
        fixture.Db.TenantMemberships.Add(membership); fixture.Db.TenantRoles.AddRange(staffRole, parentRole);
        fixture.Db.Permissions.AddRange(staffPermission, parentPermission);
        fixture.Db.RolePermissions.AddRange(new RolePermission(fixture.TenantA, staffRole.Id, staffPermission.Id), new RolePermission(fixture.TenantA, parentRole.Id, parentPermission.Id));
        fixture.Db.TenantMembershipRoles.AddRange(new TenantMembershipRole(fixture.TenantA, membership.Id, staffRole.Id), new TenantMembershipRole(fixture.TenantA, membership.Id, parentRole.Id));
        var refreshToken = new RefreshToken(Guid.NewGuid(), fixture.TenantA, person, null, new string('A', 64), fixture.Clock.UtcNow, fixture.Clock.UtcNow.AddDays(1));
        fixture.Db.RefreshTokens.Add(refreshToken);
        fixture.Db.StaffProfiles.Add(staff); fixture.Db.Guardians.Add(guardian);
        await fixture.Db.SaveChangesAsync();
        var effective = new PermissionService(fixture.Db, fixture.Context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Staff(new StaffManageWithoutRolesPermissions()).MoveToBinAsync(actor, staffId));
        Assert.Equal(staffId, (await fixture.Staff().GetAsync(actor, staffId)).Id);

        await fixture.Staff().MoveToBinAsync(actor, staffId);
        fixture.Db.ChangeTracker.Clear();
        Assert.False(await effective.HasPermissionAsync(person, Permissions.StaffView));
        Assert.True(await effective.HasPermissionAsync(person, Permissions.GuardiansView));
        Assert.NotNull(await fixture.Db.RefreshTokens.Where(x => x.Id == refreshToken.Id).Select(x => x.RevokedAtUtc).SingleAsync());
        await fixture.Staff().RestoreAsync(actor, staffId);
        fixture.Db.ChangeTracker.Clear();
        Assert.True(await effective.HasPermissionAsync(person, Permissions.StaffView));
        Assert.True(await effective.HasPermissionAsync(person, Permissions.GuardiansView));
        await fixture.Staff().MoveToBinAsync(actor, staffId);
        await fixture.Staff().PermanentlyDeleteAsync(actor, staffId);
        fixture.Db.ChangeTracker.Clear();
        Assert.True(await fixture.Db.Guardians.AnyAsync(x => x.UserId == person));
        Assert.False(await effective.HasPermissionAsync(person, Permissions.StaffView));
        Assert.True(await effective.HasPermissionAsync(person, Permissions.GuardiansView));
    }

    [Fact]
    public async Task StaffBin_SuspendsStaffOnlyMembership_AndRestoresIt()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var actor = Guid.NewGuid(); var person = Guid.NewGuid(); var staffId = Guid.NewGuid();
        var membership = new TenantMembership(Guid.NewGuid(), fixture.TenantA, person, fixture.Clock.UtcNow);
        var staff = new StaffProfile(staffId, fixture.TenantA, "ONLY-1", "Ada", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, null, null, new(2026, 9, 1), fixture.Clock.UtcNow);
        staff.LinkUser(person, fixture.Clock.UtcNow);
        fixture.Db.TenantMemberships.Add(membership); fixture.Db.StaffProfiles.Add(staff);
        await fixture.Db.SaveChangesAsync();

        await fixture.Staff().MoveToBinAsync(actor, staffId);
        fixture.Db.ChangeTracker.Clear();
        Assert.False(await fixture.Db.TenantMemberships.Where(x => x.Id == membership.Id).Select(x => x.IsActive).SingleAsync());
        await fixture.Staff().RestoreAsync(actor, staffId);
        fixture.Db.ChangeTracker.Clear();
        Assert.True(await fixture.Db.TenantMemberships.Where(x => x.Id == membership.Id).Select(x => x.IsActive).SingleAsync());
        await fixture.Staff().MoveToBinAsync(actor, staffId);
        await fixture.Staff().PermanentlyDeleteAsync(actor, staffId);
        fixture.Db.ChangeTracker.Clear();
        Assert.False(await fixture.Db.TenantMemberships.Where(x => x.Id == membership.Id).Select(x => x.IsActive).SingleAsync());
    }

    [Fact]
    public async Task StaffDocumentContent_RejectsAnotherStaffMembersUpload()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var actor = Guid.NewGuid(); var ownId = Guid.NewGuid(); var otherId = Guid.NewGuid();
        var own = new StaffProfile(ownId, fixture.TenantA, "STF-OWN", "Ada", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, "ada@example.test", "08012345678", new DateOnly(2026, 9, 1), fixture.Clock.UtcNow);
        own.LinkUser(actor, fixture.Clock.UtcNow);
        fixture.Db.StaffProfiles.AddRange(own, new StaffProfile(otherId, fixture.TenantA, "STF-OTHER", "Bola", "Eze", StaffCategory.Teaching, fixture.CampusA, null, null, "bola@example.test", "08012345679", new DateOnly(2026, 9, 1), fixture.Clock.UtcNow));
        var fileId = Guid.NewGuid();
        fixture.Db.StoredFiles.Add(new StoredFile(fileId, fixture.TenantA, "staff/other/signature.png", "signature.png", "image/png", 3, "signature", "StaffProfile", otherId, Guid.NewGuid(), fixture.Clock.UtcNow));
        await fixture.Db.SaveChangesAsync();
        var documents = new PhaseOneDocumentService(fixture.Db, new AllowedAccess(), new ViewOnlyPermissions(), new UnusedFileService());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => documents.UploadContentAsync(actor, fileId, new MemoryStream([1, 2, 3]), "image/png", 3));
    }

    [Fact]
    public async Task StaffDirectory_PhotoUrlRequiresSensitivePermissionAndAvailableFile()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var actor = Guid.NewGuid(); var staffId = Guid.NewGuid();
        var staff = new StaffProfile(staffId, fixture.TenantA, "STF-PHOTO", "Ada", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, "ada@example.test", "08012345678", new DateOnly(2026, 9, 1), fixture.Clock.UtcNow);
        staff.LinkUser(actor, fixture.Clock.UtcNow);
        fixture.Db.StaffProfiles.Add(staff);
        var photo = new StoredFile(Guid.NewGuid(), fixture.TenantA, "staff/photo.png", "photo.png", "image/png", 3, "photo", "StaffProfile", staffId, actor, fixture.Clock.UtcNow);
        photo.MarkAvailable(new string('A', 64));
        fixture.Db.StoredFiles.Add(photo);
        await fixture.Db.SaveChangesAsync();

        var withoutSensitiveAccess = await fixture.Staff(new StaffViewOnlyPermissions()).ListAsync(actor, 1, 20, null, null);
        var withSensitiveAccess = await fixture.Staff().ListAsync(actor, 1, 20, null, null);

        Assert.Null(Assert.Single(withoutSensitiveAccess.Items).PhotoUrl);
        Assert.Equal("https://files.example.test/staff/photo.png", Assert.Single(withSensitiveAccess.Items).PhotoUrl);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(GiddyEduDbContext db, TenantContextAccessor context, Guid tenantA, Guid tenantB, Guid campusA, SystemClock clock)
        { Db = db; Context = context; TenantA = tenantA; TenantB = tenantB; CampusA = campusA; Clock = clock; }
        public GiddyEduDbContext Db { get; } public TenantContextAccessor Context { get; } public Guid TenantA { get; } public Guid TenantB { get; } public Guid CampusA { get; } public SystemClock Clock { get; }
        public AcademicStructureService Academics() => new(Db, Context, new AllowedAccess(), Clock);
        public SchoolAdministrationService Schools() => new(Db, Context, new AllowedAccess(), new UnusedFileService(), Clock);
        public StaffService Staff(IPermissionService? permissions = null) => new(Db, Context, new AllowedAccess(), permissions ?? new AllowedPermissions(), Clock, new TestFileObjectStorage());
        public StudentLifecycleService Students(IPermissionService? permissions = null) => new(Db, Context, new AllowedAccess(), permissions ?? new AllowedPermissions(), new TestFileObjectStorage(), Clock);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
        public static async Task<Fixture> CreateAsync()
        {
            var context = new TenantContextAccessor(); var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var db = new GiddyEduDbContext(options, context); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var campusA = Guid.NewGuid(); var clock = new SystemClock();
            db.Tenants.AddRange(new Tenant(a, "A", $"a-{a:N}", clock.UtcNow), new Tenant(b, "B", $"b-{b:N}", clock.UtcNow)); await db.SaveChangesAsync();
            context.Set(a, null); db.Campuses.Add(new Campus(campusA, a, "Main", "MAIN", clock.UtcNow)); await db.SaveChangesAsync();
            context.Set(b, null); db.Campuses.Add(new Campus(Guid.NewGuid(), b, "Main", "MAIN", clock.UtcNow)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            return new(db, context, a, b, campusA, clock);
        }
    }
    private sealed class AllowedAccess : IFeatureAccessGuard
    { public Task DemandAsync(Guid actorUserId, string permission, string featureKey, CancellationToken ct = default) => Task.CompletedTask; }
    private sealed class DeniedAccess : IFeatureAccessGuard
    { public Task DemandAsync(Guid actorUserId, string permission, string featureKey, CancellationToken ct = default) => Task.FromException(new UnauthorizedAccessException()); }
    private sealed class AllowedPermissions : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<string>>([]);
    }
    private sealed class ViewOnlyPermissions : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(permission.EndsWith(".View", StringComparison.Ordinal));
        public Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<string>>([]);
    }
    private sealed class UnusedFileService : IFileService
    {
        public Task<FileUpload> BeginUploadAsync(string fileName, string contentType, long sizeBytes, string category, string entityType, Guid entityId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteUploadAsync(Guid fileId, string checksum, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UploadContentAsync(Guid fileId, Stream content, string contentType, long? contentLength, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> CreateDownloadUrlAsync(Guid fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class RecordingFileService : IFileService
    {
        public Guid? DeletedFileId { get; private set; }
        public Task<FileUpload> BeginUploadAsync(string fileName, string contentType, long sizeBytes, string category, string entityType, Guid entityId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteUploadAsync(Guid fileId, string checksum, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UploadContentAsync(Guid fileId, Stream content, string contentType, long? contentLength, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> CreateDownloadUrlAsync(Guid fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default) { DeletedFileId = fileId; return Task.CompletedTask; }
    }
    private sealed class StaffViewOnlyPermissions : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(permission == Permissions.StaffView);
        public Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<string>>([]);
    }
    private sealed class StaffManageWithoutRolesPermissions : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(permission == Permissions.StaffManage);
        public Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<string>>([]);
    }
    private sealed class TestFileObjectStorage : IFileObjectStorage
    {
        public string CreateUploadUrl(string objectKey, string contentType) => throw new NotSupportedException();
        public string CreateDownloadUrl(string objectKey) => $"https://files.example.test/{objectKey}";
        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UploadAsync(string objectKey, string contentType, Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> ReadTextAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<byte[]> ReadBytesAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task WriteTextAsync(string objectKey, string contentType, string value, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
