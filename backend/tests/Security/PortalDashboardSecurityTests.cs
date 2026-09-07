using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Subscriptions;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Academics.Domain;
using GiddyEdu.Modules.Hr.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.SecurityTests;

public sealed class PortalDashboardSecurityTests
{
    [Fact]
    public async Task AdministrativeMetrics_IncludeOnlyCurrentTenantRecords()
    {
        var context = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context);
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        db.Tenants.AddRange(new Tenant(tenantA, "A", $"a-{tenantA:N}", now), new Tenant(tenantB, "B", $"b-{tenantB:N}", now));
        await db.SaveChangesAsync();
        context.Set(tenantA, null); db.Campuses.Add(new Campus(Guid.NewGuid(), tenantA, "A Campus", "A", now)); await db.SaveChangesAsync();
        context.Set(tenantB, null); db.Campuses.Add(new Campus(Guid.NewGuid(), tenantB, "B Campus", "B", now)); await db.SaveChangesAsync();
        context.Set(tenantA, null); db.ChangeTracker.Clear();

        var service = new PortalDashboardService(db, new StubPermissions([Permissions.SchoolsView]), new StubProfile(["SchoolAdmin"]), new EnabledEntitlements());
        var dashboard = await service.GetAsync(Guid.NewGuid(), "SchoolAdmin");

        Assert.Equal(1, Assert.Single(dashboard.Metrics).Value);
    }

    [Fact]
    public async Task RequestedAudience_MustBelongToAuthenticatedUsersPresentation()
    {
        var context = new TenantContextAccessor(); context.Set(Guid.NewGuid(), null);
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context);
        var service = new PortalDashboardService(db, new StubPermissions([]), new StubProfile(["Parent"]), new EnabledEntitlements());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(Guid.NewGuid(), "SchoolAdmin"));
    }

    [Fact]
    public async Task TeachingClasses_IncludeOnlyAssignmentsAndLearnersLinkedToTheActor()
    {
        var context = new TenantContextAccessor(); var tenantId = Guid.NewGuid(); context.Set(tenantId, null);
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context);
        var actor = Guid.NewGuid(); var campusId = Guid.NewGuid(); var ownStaffId = Guid.NewGuid(); var otherStaffId = Guid.NewGuid();
        var ownClassId = Guid.NewGuid(); var otherClassId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var ownStaff = new StaffProfile(ownStaffId, tenantId, "T-1", "Linked", "Teacher", StaffCategory.Teaching, campusId, null, null, null, null, new(2026, 9, 1), now);
        ownStaff.LinkUser(actor, now);
        db.StaffProfiles.AddRange(ownStaff, new StaffProfile(otherStaffId, tenantId, "T-2", "Other", "Teacher", StaffCategory.Teaching, campusId, null, null, null, null, new(2026, 9, 1), now));
        db.ClassSections.AddRange(
            new ClassSection(ownClassId, tenantId, campusId, Guid.NewGuid(), Guid.NewGuid(), "JSS 1 Gold", "JSS1-G", 30, now),
            new ClassSection(otherClassId, tenantId, campusId, Guid.NewGuid(), Guid.NewGuid(), "JSS 2 Gold", "JSS2-G", 30, now));
        db.TeachingAssignments.AddRange(
            new TeachingAssignment(Guid.NewGuid(), tenantId, ownStaffId, ownClassId, null, TeachingAssignmentRole.ClassTeacher, now),
            new TeachingAssignment(Guid.NewGuid(), tenantId, otherStaffId, otherClassId, null, TeachingAssignmentRole.ClassTeacher, now));
        var ownStudentId = Guid.NewGuid(); var otherStudentId = Guid.NewGuid();
        db.Students.AddRange(
            new Student(ownStudentId, tenantId, "S-1", "Own", "Learner", new(2015, 1, 1), null, now),
            new Student(otherStudentId, tenantId, "S-2", "Other", "Learner", new(2015, 1, 1), null, now));
        db.Enrollments.AddRange(
            new Enrollment(Guid.NewGuid(), tenantId, ownStudentId, Guid.NewGuid(), ownClassId, new(2026, 9, 1), now),
            new Enrollment(Guid.NewGuid(), tenantId, otherStudentId, Guid.NewGuid(), otherClassId, new(2026, 9, 1), now));
        await db.SaveChangesAsync();
        var effectivePermissions = new[] { Permissions.AcademicsView, Permissions.StudentsView };
        var service = new PortalDashboardService(db, new StubPermissions(effectivePermissions), new StubProfile(["Teacher"]), new EnabledEntitlements());

        var result = await service.GetTeachingClassesAsync(actor);

        var assignedClass = Assert.Single(result);
        Assert.Equal(ownClassId, assignedClass.ClassSectionId);
        Assert.Equal(1, assignedClass.StudentCount);
    }

    [Fact]
    public async Task FamilyStudents_IncludeOnlyGuardianLinkedLearners()
    {
        var context = new TenantContextAccessor(); var tenantId = Guid.NewGuid(); context.Set(tenantId, null);
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context);
        var actor = Guid.NewGuid(); var guardianId = Guid.NewGuid(); var linkedStudentId = Guid.NewGuid(); var unrelatedStudentId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var guardian = new Guardian(guardianId, tenantId, "Ada", "Parent", "0801", "ada@example.test", now); guardian.LinkUser(actor);
        db.Guardians.Add(guardian);
        db.Students.AddRange(
            new Student(linkedStudentId, tenantId, "S-1", "Linked", "Child", new(2015, 1, 1), null, now),
            new Student(unrelatedStudentId, tenantId, "S-2", "Unrelated", "Child", new(2015, 1, 1), null, now));
        db.StudentGuardians.Add(new StudentGuardian(tenantId, linkedStudentId, guardianId, GuardianRelationshipType.Parent, true, true, true));
        await db.SaveChangesAsync();
        var service = new PortalDashboardService(db, new StubPermissions([Permissions.StudentsView]), new StubProfile(["Parent"]), new EnabledEntitlements());

        var result = await service.GetFamilyStudentsAsync(actor);

        Assert.Equal(linkedStudentId, Assert.Single(result).StudentId);
    }

    [Fact]
    public async Task StudentSelfService_ReturnsOnlyTheActorsLinkedRecordAndEnrollment()
    {
        var context = new TenantContextAccessor(); var tenantId = Guid.NewGuid(); context.Set(tenantId, null);
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context);
        var actor = Guid.NewGuid(); var studentId = Guid.NewGuid(); var yearId = Guid.NewGuid(); var classId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var student = new Student(studentId, tenantId, "SELF-1", "Own", "Record", new(2015, 1, 1), null, now); student.LinkUser(actor);
        db.Students.AddRange(student, new Student(Guid.NewGuid(), tenantId, "OTHER-1", "Other", "Record", new(2015, 1, 1), null, now));
        db.AcademicYears.Add(new AcademicYear(yearId, tenantId, "2026/2027", new(2026, 9, 1), new(2027, 7, 31), now));
        db.ClassSections.Add(new ClassSection(classId, tenantId, Guid.NewGuid(), yearId, Guid.NewGuid(), "JSS 1 Gold", "JSS1-G", 30, now));
        db.Enrollments.Add(new Enrollment(Guid.NewGuid(), tenantId, studentId, yearId, classId, new(2026, 9, 1), now));
        await db.SaveChangesAsync();
        var service = new PortalDashboardService(db, new StubPermissions([Permissions.StudentsView]), new StubProfile(["Student"]), new EnabledEntitlements());

        var result = await service.GetStudentSelfServiceAsync(actor);

        Assert.NotNull(result);
        Assert.Equal(studentId, result.StudentId);
        Assert.Equal(classId, result.CurrentEnrollment?.ClassSectionId);
    }

    [Fact]
    public async Task StaffSelfService_ReturnsOnlyTheActorsLinkedEmploymentRecord()
    {
        var context = new TenantContextAccessor(); var tenantId = Guid.NewGuid(); context.Set(tenantId, null);
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context);
        var actor = Guid.NewGuid(); var campusId = Guid.NewGuid(); var ownStaffId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        db.Campuses.Add(new Campus(campusId, tenantId, "Main Campus", "MAIN", now));
        var own = new StaffProfile(ownStaffId, tenantId, "OWN-1", "Own", "Staff", StaffCategory.Administrative, campusId, null, null, "own@example.test", null, new(2026, 9, 1), now);
        own.LinkUser(actor, now);
        db.StaffProfiles.AddRange(own, new StaffProfile(Guid.NewGuid(), tenantId, "OTHER-1", "Other", "Staff", StaffCategory.Administrative, campusId, null, null, null, null, new(2026, 9, 1), now));
        await db.SaveChangesAsync();
        var service = new PortalDashboardService(db, new StubPermissions([Permissions.StaffView]), new StubProfile(["Staff"]), new EnabledEntitlements());

        var result = await service.GetStaffSelfServiceAsync(actor);

        Assert.NotNull(result);
        Assert.Equal(ownStaffId, result.StaffId);
        Assert.Equal("own@example.test", result.WorkEmail);
    }

    [Fact]
    public async Task OperationalReadiness_DoesNotUseAnotherTenantsConfiguration()
    {
        var context = new TenantContextAccessor(); var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, context); var now = DateTimeOffset.UtcNow;
        context.Set(tenantB, null); db.Campuses.Add(new Campus(Guid.NewGuid(), tenantB, "Other Campus", "OTHER", now)); await db.SaveChangesAsync();
        context.Set(tenantA, null); db.ChangeTracker.Clear();
        var service = new PortalDashboardService(db, new StubPermissions([Permissions.SchoolsView]), new StubProfile(["SchoolAdmin"]), new EnabledEntitlements());

        var result = await service.GetOperationalReadinessAsync(Guid.NewGuid());

        Assert.False(result.Steps.Single(x => x.Key == "campus").Complete);
    }

    private sealed class StubPermissions(IReadOnlyCollection<string> values) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(values.Contains(permission));
        public Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(values);
    }

    private sealed class StubProfile(IReadOnlyList<string> audiences) : IAccessProfileService
    {
        public Task<AccessPresentation> GetPresentationAsync(Guid userId, IReadOnlyCollection<string> effectivePermissions, CancellationToken ct = default) =>
            Task.FromResult(new AccessPresentation([], audiences, audiences[0]));
    }

    private sealed class EnabledEntitlements : IEntitlementService
    {
        public Task<EffectiveEntitlement> GetAsync(string featureKey, Guid? campusId = null, CancellationToken cancellationToken = default) => Task.FromResult(new EffectiveEntitlement(true, null));
        public Task<bool> CanConsumeAsync(string featureKey, long quantity = 1, Guid? campusId = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RecordUsageAsync(string featureKey, long quantity = 1, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
