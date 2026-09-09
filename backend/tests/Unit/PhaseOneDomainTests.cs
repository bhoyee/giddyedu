using GiddyEdu.Modules.Academics.Domain;
using GiddyEdu.Modules.Schools.Domain;
using GiddyEdu.Modules.Hr.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Infrastructure.StudentLifecycle;
using GiddyEdu.Infrastructure.Authorization;

namespace GiddyEdu.UnitTests;

public sealed class PhaseOneDomainTests
{
    [Fact]
    public void StaffEmploymentRecord_RejectsEndBeforeStart()
    {
        Assert.Throws<ArgumentException>(() => new StaffEmploymentRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "School", "Teacher", new DateOnly(2024, 1, 1), new DateOnly(2023, 12, 31), null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StaffNextOfKin_RequiresContactDetails()
    {
        Assert.Throws<ArgumentException>(() => new StaffNextOfKin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "", "Sibling", "08000000000", null, null, true, DateTimeOffset.UtcNow));
    }
    [Fact]
    public void AcademicYear_RejectsInvalidDateRange()
    {
        var date = new DateOnly(2026, 9, 1);
        Assert.Throws<ArgumentException>(() => new AcademicYear(Guid.NewGuid(), Guid.NewGuid(), "2026/2027", date, date, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ClassSection_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassSection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "JSS 1 A", "JSS1-A", 0, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AcademicYear_CannotReactivateAfterClosing()
    {
        var year = new AcademicYear(Guid.NewGuid(), Guid.NewGuid(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31), DateTimeOffset.UtcNow);
        year.Activate(DateTimeOffset.UtcNow); year.Close(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => year.Activate(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SchoolProfile_NormalizesCountryAndCurrencyCodes()
    {
        var profile = new SchoolProfile(Guid.NewGuid(), "Giddy School", "ng", "Africa/Lagos", "ngn", DateTimeOffset.UtcNow);
        Assert.Equal("NG", profile.CountryCode); Assert.Equal("NGN", profile.CurrencyCode);
    }

    [Fact]
    public void StaffProfile_EnforcesEmploymentLifecycle()
    {
        var staff = new StaffProfile(Guid.NewGuid(), Guid.NewGuid(), "staff-1", "Ada", "Okafor", StaffCategory.Teaching,
            Guid.NewGuid(), null, null, "ada@example.test", null, new DateOnly(2026, 9, 1), DateTimeOffset.UtcNow);
        staff.Exit(new DateOnly(2027, 7, 31), DateTimeOffset.UtcNow);
        Assert.Equal("STAFF-1", staff.StaffNumber);
        Assert.Throws<InvalidOperationException>(() => staff.Reactivate(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Applicant_RequiresApprovedWorkflowBeforeConversion()
    {
        var applicant = new Applicant(Guid.NewGuid(), Guid.NewGuid(), "APP-1", "Chidi", "Eze", new(2015, 5, 10), null, null, null, null, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => applicant.MarkConverted(Guid.NewGuid(), DateTimeOffset.UtcNow));
        applicant.Transition(ApplicationStatus.Submitted, DateTimeOffset.UtcNow);
        applicant.Transition(ApplicationStatus.UnderReview, DateTimeOffset.UtcNow);
        applicant.Transition(ApplicationStatus.Offered, DateTimeOffset.UtcNow);
        applicant.Transition(ApplicationStatus.Accepted, DateTimeOffset.UtcNow);
        applicant.MarkConverted(Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(ApplicationStatus.Converted, applicant.Status);
    }

    [Fact]
    public void Enrollment_PreservesTerminalLifecycleState()
    {
        var enrollment = new Enrollment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(2026, 9, 1), DateTimeOffset.UtcNow);
        enrollment.Complete(EnrollmentStatus.Transferred);
        Assert.Equal(EnrollmentStatus.Transferred, enrollment.Status);
        Assert.Throws<InvalidOperationException>(() => enrollment.Complete(EnrollmentStatus.Completed));
    }

    [Fact]
    public void TeachingAssignment_RequiresSubjectOnlyForSubjectTeacher()
    {
        Assert.Throws<ArgumentException>(() => new TeachingAssignment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, TeachingAssignmentRole.SubjectTeacher, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new TeachingAssignment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TeachingAssignmentRole.ClassTeacher, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AdmissionReview_ValidatesAndRoundsScore()
    {
        var review = new AdmissionReview(Guid.NewGuid(), Guid.NewGuid(), 75.555m, "Screened", DateTimeOffset.UtcNow);
        Assert.Equal(75.56m, review.Score);
        Assert.Throws<ArgumentOutOfRangeException>(() => review.Update(101m, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AccountInvitation_IsSingleUseAndExpires()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = new AccountInvitation(Guid.NewGuid(), Guid.NewGuid(), InvitationTargetType.Guardian, Guid.NewGuid(), "parent@example.test", "HASH", now, now.AddDays(3));
        Assert.True(invitation.IsUsable(now)); invitation.Accept(now.AddMinutes(1));
        Assert.False(invitation.IsUsable(now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => invitation.Accept(now.AddMinutes(2)));
    }

    [Fact]
    public void StoredFile_RequiresSha256ChecksumBeforeAvailability()
    {
        var file = new StoredFile(Guid.NewGuid(), Guid.NewGuid(), "object.pdf", "document.pdf", "application/pdf", 100, "identity", "Student", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => file.MarkAvailable("not-a-checksum"));
        file.MarkAvailable(new string('a', 64));
        Assert.Equal(StoredFileStatus.Available, file.Status); Assert.Equal(new string('A', 64), file.Checksum);
    }

    [Theory]
    [InlineData("=2+2", "\"'=2+2\"")]
    [InlineData("+441234567", "\"'+441234567\"")]
    [InlineData("Ada \"Ace\"", "\"Ada \"\"Ace\"\"\"")]
    [InlineData(null, "\"\"")]
    public void CsvExport_EscapesUnsafeSpreadsheetValues(string? input, string expected)
    {
        Assert.Equal(expected, DataPortabilityService.EscapeCsvCell(input));
    }

    [Fact]
    public void CsvImport_ParsesQuotedCommasEscapedQuotesAndLineBreaks()
    {
        var rows = DataPortabilityService.ParseCsv("Name,Notes\r\n\"Ada, A.\",\"Said \"\"hello\"\"\"\r\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal("Ada, A.", rows[1][0]);
        Assert.Equal("Said \"hello\"", rows[1][1]);
    }

    [Fact]
    public void CsvImport_RejectsUnterminatedAndOversizedInput()
    {
        Assert.Throws<FormatException>(() => DataPortabilityService.ParseCsv("Name\r\n\"Ada"));
        Assert.Throws<FormatException>(() => DataPortabilityService.ParseCsv("Name\r\nAda\r\nGrace", maximumRows: 1));
    }

    [Fact]
    public void ImportOperation_EnforcesLifecycleAndCapturesCounts()
    {
        var now = DateTimeOffset.UtcNow;
        var operation = new ImportOperation(Guid.NewGuid(), Guid.NewGuid(), "Applicants", Guid.NewGuid(), now);
        Assert.Equal(ImportOperationStatus.AwaitingUpload, operation.Status);
        Assert.Throws<InvalidOperationException>(() => operation.Start(now));

        operation.Queue(Guid.NewGuid());
        operation.Start(now.AddSeconds(1));
        operation.Complete(12, 12, now.AddSeconds(2));

        Assert.Equal(ImportOperationStatus.Completed, operation.Status);
        Assert.Equal(12, operation.ImportedRows);
        Assert.Equal(0, operation.RejectedRows);
    }

    [Fact]
    public void PortalAudienceResolver_UsesResourceLinksAndPermissionsWithoutGrantingAuthorization()
    {
        var presentation = PortalAudienceResolver.Resolve(["Custom Educator"], [GiddyEdu.Modules.Identity.Permissions.StudentsView], isStaff: true, isTeacher: true, isGuardian: false);
        Assert.Equal(["Teacher", "Staff"], presentation.Audiences);
        Assert.Equal("Teacher", presentation.DefaultAudience);
        Assert.DoesNotContain("SchoolAdmin", presentation.Audiences);
    }

    [Fact]
    public void PortalAudienceResolver_SupportsFamilyAndFinanceExperienceLabels()
    {
        var presentation = PortalAudienceResolver.Resolve(["Parent", "Bursar"], [], isStaff: false, isTeacher: false, isGuardian: true);
        Assert.Contains("Parent", presentation.Audiences);
        Assert.Contains("Accountant", presentation.Audiences);
    }

    [Fact]
    public void StudentAccountLink_IsSingleRecordAndIdempotentForSameUser()
    {
        var userId = Guid.NewGuid();
        var student = new Student(Guid.NewGuid(), Guid.NewGuid(), "STU-1", "Ada", "Okafor", new(2014, 5, 1), null, DateTimeOffset.UtcNow, "ada@example.test");
        student.LinkUser(userId); student.LinkUser(userId);
        Assert.Equal(userId, student.UserId);
        Assert.Throws<InvalidOperationException>(() => student.LinkUser(Guid.NewGuid()));
    }

    [Fact]
    public void SystemRoleTemplates_DoNotGrantManagementPermissionsToEndUserRoles()
    {
        var endUserRoles = new[] { SystemRoleTemplates.Teacher, SystemRoleTemplates.Staff, SystemRoleTemplates.Parent, SystemRoleTemplates.Student, SystemRoleTemplates.Accountant };
        Assert.All(endUserRoles, role => Assert.DoesNotContain(role.Permissions, permission => permission.EndsWith(".Manage", StringComparison.Ordinal)));
        Assert.Equal(SystemRoleTemplates.TenantDefaults.Count, SystemRoleTemplates.TenantDefaults.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AdmissionOffer_AllowsOnlyOneResponseBeforeExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var offer = new AdmissionOffer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "HASH", now.AddDays(7), now);

        offer.Respond(true, now.AddDays(1));

        Assert.Equal(AdmissionResponse.Accepted, offer.Response);
        Assert.Throws<InvalidOperationException>(() => offer.Respond(false, now.AddDays(2)));
    }

    [Fact]
    public void AdmissionOffer_RejectsExpiredResponse()
    {
        var now = DateTimeOffset.UtcNow;
        var offer = new AdmissionOffer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "HASH", now.AddDays(1), now);

        Assert.Throws<InvalidOperationException>(() => offer.Respond(true, now.AddDays(2)));
        Assert.Equal(AdmissionResponse.Pending, offer.Response);
    }

    [Fact]
    public void ReturningStudent_CanReactivateAfterWithdrawal()
    {
        var withdrawn = new Student(Guid.NewGuid(), Guid.NewGuid(), "RET-1", "Ada", "Okafor", new(2012, 1, 1), null, DateTimeOffset.UtcNow);
        withdrawn.Withdraw(); withdrawn.ReactivateForReturn();
        Assert.Equal(StudentStatus.Active, withdrawn.Status);
    }

    [Fact]
    public void Graduation_CompletesOnlyActiveStudentLifecycle()
    {
        var student = new Student(Guid.NewGuid(), Guid.NewGuid(), "GRAD-1", "Ada", "Okafor", new(2010, 1, 1), null, DateTimeOffset.UtcNow);
        student.Graduate();
        Assert.Equal(StudentStatus.Graduated, student.Status);
        Assert.Throws<InvalidOperationException>(student.Graduate);
    }

    [Fact]
    public void StudentProgression_RequiresDistinctHistoricalEnrollments()
    {
        var progression = new StudentProgression(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), StudentProgressionType.Promotion, "Advanced to the next level", Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(StudentProgressionType.Promotion, progression.Type);
        Assert.Equal("Advanced to the next level", progression.Reason);
    }
}
