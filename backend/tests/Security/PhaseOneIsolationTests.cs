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
using GiddyEdu.Infrastructure.StudentLifecycle;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.SecurityTests;

public sealed class PhaseOneIsolationTests
{
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
        var yearB = await fixture.Academics().CreateAcademicYearAsync(Guid.NewGuid(), new("B Year", new(2026, 9, 1), new(2027, 7, 31)));
        var stageB = await fixture.Academics().CreateEducationStageAsync(Guid.NewGuid(), new("Secondary", "SEC", 1));
        var levelB = await fixture.Academics().CreateClassLevelAsync(Guid.NewGuid(), new(stageB, "JSS 1", "JSS1", 1));
        fixture.Db.ChangeTracker.Clear(); fixture.Context.Set(fixture.TenantA, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Academics().CreateClassSectionAsync(Guid.NewGuid(), new(fixture.CampusA, yearB, levelB, "JSS 1 A", "JSS1-A", 30)));
    }

    [Fact]
    public async Task Services_RejectActorsWithoutRequiredPermissions()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var deniedAcademics = new AcademicStructureService(fixture.Db, fixture.Context, new DeniedAccess(), fixture.Clock);
        var deniedSchools = new SchoolAdministrationService(fixture.Db, fixture.Context, new DeniedAccess(), fixture.Clock);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => deniedAcademics.GetAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => deniedSchools.ListCampusesAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SchoolAdministration_PreservesAtLeastOneActiveCampus()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Schools().DeactivateCampusAsync(Guid.NewGuid(), fixture.CampusA));
    }

    [Fact]
    public async Task AcademicStructure_CreatesNigerianStructureWithoutHardCodingIt()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null); var service = fixture.Academics();
        var year = await service.CreateAcademicYearAsync(Guid.NewGuid(), new("2026/2027", new(2026, 9, 1), new(2027, 7, 31)));
        await service.CreateTermAsync(Guid.NewGuid(), new(year, "First Term", "T1", 1, new(2026, 9, 1), new(2026, 12, 18)));
        var stage = await service.CreateEducationStageAsync(Guid.NewGuid(), new("Junior Secondary", "JSS", 3));
        var level = await service.CreateClassLevelAsync(Guid.NewGuid(), new(stage, "JSS 1", "JSS1", 1));
        var section = await service.CreateClassSectionAsync(Guid.NewGuid(), new(fixture.CampusA, year, level, "JSS 1 Gold", "JSS1-GOLD", 35));
        var subject = await service.CreateSubjectAsync(Guid.NewGuid(), new(null, "Mathematics", "MATH", true));
        await service.AssignSubjectAsync(Guid.NewGuid(), new(section, subject, true));
        var result = await service.GetAsync(Guid.NewGuid(), year);
        Assert.Single(result.Terms); Assert.Single(result.ClassSections); Assert.Single(result.ClassSubjects);
    }

    [Fact]
    public async Task StaffManagement_IsTenantIsolated()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Set(fixture.TenantA, null);
        await fixture.Staff().CreateAsync(Guid.NewGuid(), new("A-001", "Ada", "Okafor", StaffCategory.Teaching, fixture.CampusA, null, null, null, null, new(2026, 9, 1)));
        fixture.Context.Set(fixture.TenantB, null);
        var result = await fixture.Staff().ListAsync(Guid.NewGuid(), 1, 25, null, null);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task StaffManagement_RejectsCrossTenantCampus()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantB, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Staff().CreateAsync(Guid.NewGuid(), new("B-001", "Bola", "Ade", StaffCategory.Administrative, fixture.CampusA, null, null, null, null, new(2026, 9, 1))));
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
        await fixture.Students().CreateApplicantAsync(Guid.NewGuid(), new("APP-A", "Ngozi", "Ibe", new(2015, 1, 1), null, null, null, null));
        fixture.Context.Set(fixture.TenantB, null);
        var result = await fixture.Students().ListApplicantsAsync(Guid.NewGuid(), 1, 25, null);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ImportOperations_AreTenantIsolated()
    {
        await using var fixture = await Fixture.CreateAsync(); fixture.Context.Set(fixture.TenantA, null);
        var applicantImportId = Guid.NewGuid(); var studentImportId = Guid.NewGuid(); var guardianImportId = Guid.NewGuid();
        fixture.Db.ImportOperations.AddRange(
            new ImportOperation(applicantImportId, fixture.TenantA, "Applicants", Guid.NewGuid(), fixture.Clock.UtcNow),
            new ImportOperation(studentImportId, fixture.TenantA, "Students", Guid.NewGuid(), fixture.Clock.UtcNow),
            new ImportOperation(guardianImportId, fixture.TenantA, "Guardians", Guid.NewGuid(), fixture.Clock.UtcNow));
        await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear(); fixture.Context.Set(fixture.TenantB, null);
        Assert.Empty(await fixture.Db.ImportOperations.ToListAsync());
        var applicants = new ApplicantImportService(fixture.Db, fixture.Context, new AllowedAccess(), null!, fixture.Clock, null!);
        var profiles = new ProfileImportService(fixture.Db, fixture.Context, new AllowedAccess(), null!, fixture.Clock, null!);
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
        fixture.Db.StudentSensitiveRecords.Add(new StudentSensitiveRecord(fixture.TenantA, studentId, "SECRET-STUDENT-ADDRESS", "SECRET-STUDENT-MEDICAL", null, null, "SECRET-PRIVATE-NOTE", now));
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
        fixture.Db.StudentSensitiveRecords.AddRange(new StudentSensitiveRecord(fixture.TenantA, ownId, "Own address", null, null, null, null, fixture.Clock.UtcNow), new StudentSensitiveRecord(fixture.TenantA, otherId, "Other address", null, null, null, null, fixture.Clock.UtcNow)); await fixture.Db.SaveChangesAsync();
        var permissions = new ViewOnlyPermissions(); var students = fixture.Students(permissions);
        var documents = new PhaseOneDocumentService(fixture.Db, new AllowedAccess(), permissions, new UnusedFileService());

        Assert.Equal("Own address", (await students.GetStudentSensitiveAsync(actor, ownId))?.Address);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => students.GetStudentSensitiveAsync(actor, otherId));
        Assert.Empty(await documents.ListAsync(actor, "Student", ownId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => documents.ListAsync(actor, "Student", otherId));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(GiddyEduDbContext db, TenantContextAccessor context, Guid tenantA, Guid tenantB, Guid campusA, SystemClock clock)
        { Db = db; Context = context; TenantA = tenantA; TenantB = tenantB; CampusA = campusA; Clock = clock; }
        public GiddyEduDbContext Db { get; } public TenantContextAccessor Context { get; } public Guid TenantA { get; } public Guid TenantB { get; } public Guid CampusA { get; } public SystemClock Clock { get; }
        public AcademicStructureService Academics() => new(Db, Context, new AllowedAccess(), Clock);
        public SchoolAdministrationService Schools() => new(Db, Context, new AllowedAccess(), Clock);
        public StaffService Staff(IPermissionService? permissions = null) => new(Db, Context, new AllowedAccess(), permissions ?? new AllowedPermissions(), Clock);
        public StudentLifecycleService Students(IPermissionService? permissions = null) => new(Db, Context, new AllowedAccess(), permissions ?? new AllowedPermissions(), Clock);
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
        public Task<string> CreateDownloadUrlAsync(Guid fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
