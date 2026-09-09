using GiddyEdu.BuildingBlocks.Api;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Academics.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

public sealed record ApplicantInput(string ApplicationNumber, string FirstName, string LastName, DateOnly DateOfBirth, string? Email, string? Phone, string? PreviousSchool, string? Source);
public sealed record ApplicationTransitionInput(ApplicationStatus Status);
public sealed record ApplicantInfo(Guid Id, string ApplicationNumber, string FirstName, string LastName, DateOnly DateOfBirth, ApplicationStatus Status, Guid? StudentId);
public sealed record ConvertApplicantInput(string AdmissionNumber, Guid AcademicYearId, Guid ClassSectionId, DateOnly EnrolledOn);
public sealed record StudentInfo(Guid Id, string AdmissionNumber, string FirstName, string LastName, DateOnly DateOfBirth, string? Email, StudentStatus Status);
public sealed record StudentProfileInput(string FirstName, string LastName, DateOnly DateOfBirth, string? Email = null);
public sealed record GuardianInput(string FirstName, string LastName, string Phone, string? Email);
public sealed record GuardianLinkInput(Guid GuardianId, GuardianRelationshipType Relationship, bool IsPrimary, bool IsEmergencyContact, bool MayCollect);
public sealed record GuardianInfo(Guid Id, string FirstName, string LastName, string Phone, string? Email);
public sealed record EnrollmentInfo(Guid Id, Guid AcademicYearId, Guid ClassSectionId, DateOnly EnrolledOn, EnrollmentStatus Status);
public sealed record EnrollmentInput(Guid AcademicYearId, Guid ClassSectionId, DateOnly EnrolledOn, EnrollmentStatus PreviousEnrollmentStatus = EnrollmentStatus.Completed);
public sealed record StudentProgressionInput(StudentProgressionType Type, Guid AcademicYearId, Guid ClassSectionId, DateOnly EffectiveOn, string? Reason);
public sealed record StudentDetail(StudentInfo Student, IReadOnlyList<GuardianInfo> Guardians, IReadOnlyList<EnrollmentInfo> Enrollments);
public sealed record ApplicantSensitiveInput(string? Address, string? MedicalInformation, string? Allergies, string? SpecialEducationalNeeds);
public sealed record ApplicantSensitiveInfo(string? Address, string? MedicalInformation, string? Allergies, string? SpecialEducationalNeeds, DateTimeOffset UpdatedAtUtc);
public sealed record StudentSensitiveInput(string? Address, string? MedicalInformation, string? Allergies, string? SpecialEducationalNeeds, string? PrivateNotes);
public sealed record StudentSensitiveInfo(string? Address, string? MedicalInformation, string? Allergies, string? SpecialEducationalNeeds, string? PrivateNotes, DateTimeOffset UpdatedAtUtc);
public sealed record AdmissionReviewInput(decimal Score, string? ScreeningNotes);
public sealed record AdmissionReviewInfo(decimal Score, string? ScreeningNotes, DateTimeOffset UpdatedAtUtc);
public sealed record AdmissionInterviewInput(DateTimeOffset ScheduledAtUtc, string? Location);
public sealed record AdmissionInterviewOutcomeInput(InterviewStatus Status, string? OutcomeNotes);
public sealed record AdmissionInterviewInfo(Guid Id, DateTimeOffset ScheduledAtUtc, string? Location, InterviewStatus Status, string? OutcomeNotes);

public interface IStudentLifecycleService
{
    Task<Guid> CreateApplicantAsync(Guid actor, ApplicantInput input, CancellationToken ct = default);
    Task<PageResult<ApplicantInfo>> ListApplicantsAsync(Guid actor, int page, int pageSize, ApplicationStatus? status, CancellationToken ct = default);
    Task TransitionAsync(Guid actor, Guid applicantId, ApplicationTransitionInput input, CancellationToken ct = default);
    Task<ApplicantSensitiveInfo?> GetApplicantSensitiveAsync(Guid actor, Guid applicantId, CancellationToken ct = default);
    Task UpsertApplicantSensitiveAsync(Guid actor, Guid applicantId, ApplicantSensitiveInput input, CancellationToken ct = default);
    Task<AdmissionReviewInfo?> GetAdmissionReviewAsync(Guid actor, Guid applicantId, CancellationToken ct = default);
    Task UpsertAdmissionReviewAsync(Guid actor, Guid applicantId, AdmissionReviewInput input, CancellationToken ct = default);
    Task<IReadOnlyList<AdmissionInterviewInfo>> ListAdmissionInterviewsAsync(Guid actor, Guid applicantId, CancellationToken ct = default);
    Task<Guid> ScheduleAdmissionInterviewAsync(Guid actor, Guid applicantId, AdmissionInterviewInput input, CancellationToken ct = default);
    Task CompleteAdmissionInterviewAsync(Guid actor, Guid interviewId, AdmissionInterviewOutcomeInput input, CancellationToken ct = default);
    Task<Guid> ConvertAsync(Guid actor, Guid applicantId, ConvertApplicantInput input, CancellationToken ct = default);
    Task<PageResult<StudentInfo>> ListStudentsAsync(Guid actor, int page, int pageSize, string? search, CancellationToken ct = default);
    Task<StudentDetail> GetStudentAsync(Guid actor, Guid studentId, CancellationToken ct = default);
    Task UpdateStudentAsync(Guid actor, Guid studentId, StudentProfileInput input, CancellationToken ct = default);
    Task<Guid> EnrollStudentAsync(Guid actor, Guid studentId, EnrollmentInput input, CancellationToken ct = default);
    Task<Guid> ReEnrollStudentAsync(Guid actor, Guid studentId, EnrollmentInput input, CancellationToken ct = default);
    Task<Guid> ProgressStudentAsync(Guid actor, Guid studentId, StudentProgressionInput input, CancellationToken ct = default);
    Task CompleteCurrentEnrollmentAsync(Guid actor, Guid studentId, CancellationToken ct = default);
    Task GraduateStudentAsync(Guid actor, Guid studentId, CancellationToken ct = default);
    Task WithdrawStudentAsync(Guid actor, Guid studentId, CancellationToken ct = default);
    Task<StudentSensitiveInfo?> GetStudentSensitiveAsync(Guid actor, Guid studentId, CancellationToken ct = default);
    Task UpsertStudentSensitiveAsync(Guid actor, Guid studentId, StudentSensitiveInput input, CancellationToken ct = default);
    Task<Guid> CreateGuardianAsync(Guid actor, GuardianInput input, CancellationToken ct = default);
    Task<GuardianInfo> GetGuardianAsync(Guid actor, Guid guardianId, CancellationToken ct = default);
    Task UpdateGuardianAsync(Guid actor, Guid guardianId, GuardianInput input, CancellationToken ct = default);
    Task<PageResult<GuardianInfo>> ListGuardiansAsync(Guid actor, int page, int pageSize, string? search, CancellationToken ct = default);
    Task LinkGuardianAsync(Guid actor, Guid studentId, GuardianLinkInput input, CancellationToken ct = default);
}

public sealed class StudentLifecycleService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IPermissionService permissionService, IClock clock) : IStudentLifecycleService
{
    public async Task<Guid> CreateApplicantAsync(Guid actor, ApplicantInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.AdmissionsManage, ct); var id = Guid.NewGuid(); db.Applicants.Add(new Applicant(id, RequireTenant(), input.ApplicationNumber, input.FirstName, input.LastName, input.DateOfBirth, input.Email, input.Phone, input.PreviousSchool, input.Source, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task<PageResult<ApplicantInfo>> ListApplicantsAsync(Guid actor, int page, int pageSize, ApplicationStatus? status, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.AdmissionsView, ct); RequireTenant(); page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100); var q = db.Applicants.AsNoTracking().Where(x => !status.HasValue || x.Status == status); var total = await q.LongCountAsync(ct); var items = await q.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new ApplicantInfo(x.Id, x.ApplicationNumber, x.FirstName, x.LastName, x.DateOfBirth, x.Status, x.StudentId)).ToListAsync(ct); return new(items, page, pageSize, total); }

    public async Task TransitionAsync(Guid actor, Guid applicantId, ApplicationTransitionInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.AdmissionsManage, ct); if (input.Status is ApplicationStatus.Offered or ApplicationStatus.Accepted or ApplicationStatus.Rejected or ApplicationStatus.Converted) throw new InvalidOperationException("Offer, acceptance, rejection, and conversion must use their controlled workflows."); var applicant = await db.Applicants.SingleOrDefaultAsync(x => x.Id == applicantId, ct) ?? throw new KeyNotFoundException("Applicant was not found."); applicant.Transition(input.Status, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<ApplicantSensitiveInfo?> GetApplicantSensitiveAsync(Guid actor, Guid applicantId, CancellationToken ct = default)
    { await access.DemandAsync(actor, Permissions.AdmissionsSensitiveView, FeatureKeys.Admissions, ct); if (!await db.Applicants.AnyAsync(x => x.Id == applicantId, ct)) throw new KeyNotFoundException("Applicant was not found."); return await db.ApplicantSensitiveRecords.AsNoTracking().Where(x => x.ApplicantId == applicantId).Select(x => new ApplicantSensitiveInfo(x.Address, x.MedicalInformation, x.Allergies, x.SpecialEducationalNeeds, x.UpdatedAtUtc)).SingleOrDefaultAsync(ct); }

    public async Task UpsertApplicantSensitiveAsync(Guid actor, Guid applicantId, ApplicantSensitiveInput input, CancellationToken ct = default)
    { await access.DemandAsync(actor, Permissions.AdmissionsSensitiveManage, FeatureKeys.Admissions, ct); if (!await db.Applicants.AnyAsync(x => x.Id == applicantId, ct)) throw new KeyNotFoundException("Applicant was not found."); var record = await db.ApplicantSensitiveRecords.SingleOrDefaultAsync(x => x.ApplicantId == applicantId, ct); if (record is null) db.ApplicantSensitiveRecords.Add(new ApplicantSensitiveRecord(RequireTenant(), applicantId, input.Address, input.MedicalInformation, input.Allergies, input.SpecialEducationalNeeds, clock.UtcNow)); else record.Update(input.Address, input.MedicalInformation, input.Allergies, input.SpecialEducationalNeeds, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<AdmissionReviewInfo?> GetAdmissionReviewAsync(Guid actor, Guid applicantId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.AdmissionsView, ct); if (!await db.Applicants.AnyAsync(x => x.Id == applicantId, ct)) throw new KeyNotFoundException("Applicant was not found."); return await db.AdmissionReviews.AsNoTracking().Where(x => x.ApplicantId == applicantId).Select(x => new AdmissionReviewInfo(x.Score, x.ScreeningNotes, x.UpdatedAtUtc)).SingleOrDefaultAsync(ct); }

    public async Task UpsertAdmissionReviewAsync(Guid actor, Guid applicantId, AdmissionReviewInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.AdmissionsManage, ct); var applicant = await db.Applicants.SingleOrDefaultAsync(x => x.Id == applicantId, ct) ?? throw new KeyNotFoundException("Applicant was not found."); if (applicant.Status is not (ApplicationStatus.Submitted or ApplicationStatus.UnderReview or ApplicationStatus.Waitlisted)) throw new InvalidOperationException("Only submitted, under-review, or waitlisted applications can be screened."); if (applicant.Status == ApplicationStatus.Submitted) applicant.Transition(ApplicationStatus.UnderReview, clock.UtcNow); var review = await db.AdmissionReviews.SingleOrDefaultAsync(x => x.ApplicantId == applicantId, ct); if (review is null) db.AdmissionReviews.Add(new AdmissionReview(RequireTenant(), applicantId, input.Score, input.ScreeningNotes, clock.UtcNow)); else review.Update(input.Score, input.ScreeningNotes, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<AdmissionInterviewInfo>> ListAdmissionInterviewsAsync(Guid actor, Guid applicantId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.AdmissionsView, ct); if (!await db.Applicants.AnyAsync(x => x.Id == applicantId, ct)) throw new KeyNotFoundException("Applicant was not found."); return await db.AdmissionInterviews.AsNoTracking().Where(x => x.ApplicantId == applicantId).OrderByDescending(x => x.ScheduledAtUtc).Select(x => new AdmissionInterviewInfo(x.Id, x.ScheduledAtUtc, x.Location, x.Status, x.OutcomeNotes)).ToListAsync(ct); }

    public async Task<Guid> ScheduleAdmissionInterviewAsync(Guid actor, Guid applicantId, AdmissionInterviewInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.AdmissionsManage, ct); if (!await db.Applicants.AnyAsync(x => x.Id == applicantId && x.Status != ApplicationStatus.Converted && x.Status != ApplicationStatus.Withdrawn && x.Status != ApplicationStatus.Rejected, ct)) throw new InvalidOperationException("The applicant is not available for interview."); if (input.ScheduledAtUtc <= clock.UtcNow) throw new ArgumentException("Interview time must be in the future.", nameof(input)); var id = Guid.NewGuid(); db.AdmissionInterviews.Add(new AdmissionInterview(id, RequireTenant(), applicantId, input.ScheduledAtUtc, input.Location, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task CompleteAdmissionInterviewAsync(Guid actor, Guid interviewId, AdmissionInterviewOutcomeInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.AdmissionsManage, ct); var interview = await db.AdmissionInterviews.SingleOrDefaultAsync(x => x.Id == interviewId, ct) ?? throw new KeyNotFoundException("Admission interview was not found."); interview.Complete(input.Status, input.OutcomeNotes, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<Guid> ConvertAsync(Guid actor, Guid applicantId, ConvertApplicantInput input, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.AdmissionsManage, ct); await access.DemandAsync(actor, Permissions.StudentsManage, FeatureKeys.StudentInformation, ct);
        var applicant = await db.Applicants.SingleOrDefaultAsync(x => x.Id == applicantId, ct) ?? throw new KeyNotFoundException("Applicant was not found.");
        if (!await db.ClassSections.AnyAsync(x => x.Id == input.ClassSectionId && x.AcademicYearId == input.AcademicYearId && x.IsActive, ct)) throw new InvalidOperationException("The class section and academic year must belong to the current tenant and match.");
        var id = Guid.NewGuid(); db.Students.Add(new Student(id, RequireTenant(), input.AdmissionNumber, applicant.FirstName, applicant.LastName, applicant.DateOfBirth, applicant.Id, clock.UtcNow, applicant.Email)); db.Enrollments.Add(new Enrollment(Guid.NewGuid(), RequireTenant(), id, input.AcademicYearId, input.ClassSectionId, input.EnrolledOn, clock.UtcNow)); var sensitive = await db.ApplicantSensitiveRecords.AsNoTracking().SingleOrDefaultAsync(x => x.ApplicantId == applicantId, ct); if (sensitive is not null) db.StudentSensitiveRecords.Add(new StudentSensitiveRecord(RequireTenant(), id, sensitive.Address, sensitive.MedicalInformation, sensitive.Allergies, sensitive.SpecialEducationalNeeds, null, clock.UtcNow)); applicant.MarkConverted(id, clock.UtcNow); await db.SaveChangesAsync(ct); return id;
    }

    public async Task<PageResult<StudentInfo>> ListStudentsAsync(Guid actor, int page, int pageSize, string? search, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StudentsView, ct); RequireTenant(); page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100); var q = await ScopedStudentsAsync(actor, ct); if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => x.AdmissionNumber.Contains(s) || x.FirstName.Contains(s) || x.LastName.Contains(s)); } var total = await q.LongCountAsync(ct); var items = await q.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new StudentInfo(x.Id, x.AdmissionNumber, x.FirstName, x.LastName, x.DateOfBirth, x.Email, x.Status)).ToListAsync(ct); return new(items, page, pageSize, total); }

    public async Task<StudentDetail> GetStudentAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StudentsView, ct); RequireTenant(); var canManage = await permissionService.HasPermissionAsync(actor, Permissions.StudentsManage, ct); var student = await (await ScopedStudentsAsync(actor, ct)).Where(x => x.Id == studentId).Select(x => new StudentInfo(x.Id, x.AdmissionNumber, x.FirstName, x.LastName, x.DateOfBirth, x.Email, x.Status)).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Student was not found.");
        var guardians = await (from link in db.StudentGuardians join guardian in db.Guardians on link.GuardianId equals guardian.Id where link.StudentId == studentId && (canManage || guardian.UserId == actor) select new GuardianInfo(guardian.Id, guardian.FirstName, guardian.LastName, guardian.Phone, guardian.Email)).ToListAsync(ct);
        var enrollments = await db.Enrollments.AsNoTracking().Where(x => x.StudentId == studentId).OrderByDescending(x => x.EnrolledOn).Select(x => new EnrollmentInfo(x.Id, x.AcademicYearId, x.ClassSectionId, x.EnrolledOn, x.Status)).ToListAsync(ct);
        return new(student, guardians, enrollments);
    }

    public async Task<Guid> EnrollStudentAsync(Guid actor, Guid studentId, EnrollmentInput input, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StudentsManage, ct); var student = await db.Students.SingleOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new KeyNotFoundException("Student was not found.");
        if (student.Status != StudentStatus.Active) throw new InvalidOperationException("Only active students can be enrolled.");
        if (!await db.ClassSections.AnyAsync(x => x.Id == input.ClassSectionId && x.AcademicYearId == input.AcademicYearId && x.IsActive, ct)) throw new InvalidOperationException("The class section and academic year must belong to the current tenant and match.");
        var active = await db.Enrollments.Where(x => x.StudentId == studentId && x.Status == EnrollmentStatus.Active).ToListAsync(ct);
        if (active.Count > 0 && input.PreviousEnrollmentStatus == EnrollmentStatus.Active) throw new ArgumentException("A terminal status is required for the previous enrolment.", nameof(input));
        foreach (var enrollment in active) enrollment.Complete(input.PreviousEnrollmentStatus);
        var id = Guid.NewGuid(); db.Enrollments.Add(new Enrollment(id, RequireTenant(), studentId, input.AcademicYearId, input.ClassSectionId, input.EnrolledOn, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task<Guid> ReEnrollStudentAsync(Guid actor, Guid studentId, EnrollmentInput input, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StudentsManage, ct);
        var student = await db.Students.SingleOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new KeyNotFoundException("Student was not found.");
        if (!await db.Enrollments.AnyAsync(x => x.StudentId == studentId, ct)) throw new InvalidOperationException("Returning-student registration requires an existing enrolment history.");
        if (await db.Enrollments.AnyAsync(x => x.StudentId == studentId && x.Status == EnrollmentStatus.Active, ct)) throw new InvalidOperationException("The student already has an active enrolment.");
        if (!await (from section in db.ClassSections join year in db.AcademicYears on section.AcademicYearId equals year.Id where section.Id == input.ClassSectionId && section.AcademicYearId == input.AcademicYearId && section.IsActive && year.Status == AcademicPeriodStatus.Active select section.Id).AnyAsync(ct)) throw new InvalidOperationException("Re-enrolment requires an active academic year and matching class section in the current tenant.");
        student.ReactivateForReturn(); var id = Guid.NewGuid(); db.Enrollments.Add(new Enrollment(id, RequireTenant(), studentId, input.AcademicYearId, input.ClassSectionId, input.EnrolledOn, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task<Guid> ProgressStudentAsync(Guid actor, Guid studentId, StudentProgressionInput input, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StudentsManage, ct); var tenantId = RequireTenant();
        var student = await db.Students.SingleOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new KeyNotFoundException("Student was not found.");
        if (student.Status != StudentStatus.Active) throw new InvalidOperationException("Only active students can be progressed or transferred.");
        var current = await db.Enrollments.SingleOrDefaultAsync(x => x.StudentId == studentId && x.Status == EnrollmentStatus.Active, ct) ?? throw new InvalidOperationException("The student does not have an active enrolment.");
        var currentLevel = await db.ClassSections.Where(x => x.Id == current.ClassSectionId).Select(x => x.ClassLevelId).SingleAsync(ct);
        var target = await (from section in db.ClassSections join year in db.AcademicYears on section.AcademicYearId equals year.Id where section.Id == input.ClassSectionId && section.AcademicYearId == input.AcademicYearId && section.IsActive && year.Status == AcademicPeriodStatus.Active select new { section.Id, section.ClassLevelId }).SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("The target must be an active class in the active academic year.");
        if (input.Type == StudentProgressionType.Promotion && target.ClassLevelId == currentLevel) throw new InvalidOperationException("Promotion must move the student to a different class level.");
        if (input.Type == StudentProgressionType.RepeatClass && target.ClassLevelId != currentLevel) throw new InvalidOperationException("Repeat-class processing must retain the current class level.");
        if (input.Type == StudentProgressionType.Transfer && target.Id == current.ClassSectionId) throw new InvalidOperationException("Transfer must move the student to another class section.");
        current.Complete(input.Type == StudentProgressionType.Transfer ? EnrollmentStatus.Transferred : EnrollmentStatus.Completed);
        var nextId = Guid.NewGuid(); db.Enrollments.Add(new Enrollment(nextId, tenantId, studentId, input.AcademicYearId, input.ClassSectionId, input.EffectiveOn, clock.UtcNow));
        var progressionId = Guid.NewGuid(); db.StudentProgressions.Add(new StudentProgression(progressionId, tenantId, studentId, current.Id, nextId, input.Type, input.Reason, actor, clock.UtcNow)); await db.SaveChangesAsync(ct); return progressionId;
    }

    public async Task CompleteCurrentEnrollmentAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StudentsManage, ct); if (!await db.Students.AnyAsync(x => x.Id == studentId && x.Status == StudentStatus.Active, ct)) throw new KeyNotFoundException("Active student was not found."); var enrollment = await db.Enrollments.SingleOrDefaultAsync(x => x.StudentId == studentId && x.Status == EnrollmentStatus.Active, ct) ?? throw new InvalidOperationException("The student does not have an active enrolment."); enrollment.Complete(EnrollmentStatus.Completed); await db.SaveChangesAsync(ct); }

    public async Task GraduateStudentAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StudentsManage, ct); var student = await db.Students.SingleOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new KeyNotFoundException("Student was not found."); var enrollment = await db.Enrollments.SingleOrDefaultAsync(x => x.StudentId == studentId && x.Status == EnrollmentStatus.Active, ct) ?? throw new InvalidOperationException("Graduation requires an active enrolment."); enrollment.Complete(EnrollmentStatus.Completed); student.Graduate(); await db.SaveChangesAsync(ct); }

    public async Task UpdateStudentAsync(Guid actor, Guid studentId, StudentProfileInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StudentsManage, ct); var student = await db.Students.SingleOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new KeyNotFoundException("Student was not found."); student.UpdatePersonalInformation(input.FirstName, input.LastName, input.DateOfBirth, input.Email); await db.SaveChangesAsync(ct); }

    public async Task WithdrawStudentAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StudentsManage, ct); var student = await db.Students.SingleOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new KeyNotFoundException("Student was not found."); student.Withdraw(); var active = await db.Enrollments.Where(x => x.StudentId == studentId && x.Status == EnrollmentStatus.Active).ToListAsync(ct); foreach (var enrollment in active) enrollment.Complete(EnrollmentStatus.Withdrawn); await db.SaveChangesAsync(ct); }

    public async Task<StudentSensitiveInfo?> GetStudentSensitiveAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    { await access.DemandAsync(actor, Permissions.StudentsSensitiveView, FeatureKeys.StudentInformation, ct); if (!await (await ScopedStudentsAsync(actor, ct)).AnyAsync(x => x.Id == studentId, ct)) throw new KeyNotFoundException("Student was not found."); return await db.StudentSensitiveRecords.AsNoTracking().Where(x => x.StudentId == studentId).Select(x => new StudentSensitiveInfo(x.Address, x.MedicalInformation, x.Allergies, x.SpecialEducationalNeeds, x.PrivateNotes, x.UpdatedAtUtc)).SingleOrDefaultAsync(ct); }

    public async Task UpsertStudentSensitiveAsync(Guid actor, Guid studentId, StudentSensitiveInput input, CancellationToken ct = default)
    { await access.DemandAsync(actor, Permissions.StudentsSensitiveManage, FeatureKeys.StudentInformation, ct); if (!await (await ScopedStudentsAsync(actor, ct)).AnyAsync(x => x.Id == studentId, ct)) throw new KeyNotFoundException("Student was not found."); var record = await db.StudentSensitiveRecords.SingleOrDefaultAsync(x => x.StudentId == studentId, ct); if (record is null) db.StudentSensitiveRecords.Add(new StudentSensitiveRecord(RequireTenant(), studentId, input.Address, input.MedicalInformation, input.Allergies, input.SpecialEducationalNeeds, input.PrivateNotes, clock.UtcNow)); else record.Update(input.Address, input.MedicalInformation, input.Allergies, input.SpecialEducationalNeeds, input.PrivateNotes, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<Guid> CreateGuardianAsync(Guid actor, GuardianInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.GuardiansManage, ct); var id = Guid.NewGuid(); db.Guardians.Add(new Guardian(id, RequireTenant(), input.FirstName, input.LastName, input.Phone, input.Email, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task<GuardianInfo> GetGuardianAsync(Guid actor, Guid guardianId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.GuardiansView, ct); var query = db.Guardians.AsNoTracking().Where(x => x.Id == guardianId); if (!await permissionService.HasPermissionAsync(actor, Permissions.GuardiansManage, ct)) query = query.Where(x => x.UserId == actor); return await query.Select(x => new GuardianInfo(x.Id, x.FirstName, x.LastName, x.Phone, x.Email)).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Guardian was not found."); }

    public async Task UpdateGuardianAsync(Guid actor, Guid guardianId, GuardianInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.GuardiansManage, ct); var guardian = await db.Guardians.SingleOrDefaultAsync(x => x.Id == guardianId, ct) ?? throw new KeyNotFoundException("Guardian was not found."); guardian.Update(input.FirstName, input.LastName, input.Phone, input.Email); await db.SaveChangesAsync(ct); }

    public async Task<PageResult<GuardianInfo>> ListGuardiansAsync(Guid actor, int page, int pageSize, string? search, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.GuardiansView, ct); RequireTenant(); page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100); var query = db.Guardians.AsNoTracking(); if (!await permissionService.HasPermissionAsync(actor, Permissions.GuardiansManage, ct)) query = query.Where(x => x.UserId == actor); if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => x.FirstName.Contains(term) || x.LastName.Contains(term) || x.Phone.Contains(term)); } var total = await query.LongCountAsync(ct); var items = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new GuardianInfo(x.Id, x.FirstName, x.LastName, x.Phone, x.Email)).ToListAsync(ct); return new(items, page, pageSize, total); }

    public async Task LinkGuardianAsync(Guid actor, Guid studentId, GuardianLinkInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.GuardiansManage, ct); if (!await db.Students.AnyAsync(x => x.Id == studentId, ct) || !await db.Guardians.AnyAsync(x => x.Id == input.GuardianId, ct)) throw new InvalidOperationException("Student and guardian must belong to the current tenant."); if (input.IsPrimary && await db.StudentGuardians.AnyAsync(x => x.StudentId == studentId && x.IsPrimary, ct)) throw new InvalidOperationException("A student can have only one primary guardian."); if (await db.StudentGuardians.AnyAsync(x => x.StudentId == studentId && x.GuardianId == input.GuardianId, ct)) return; db.StudentGuardians.Add(new StudentGuardian(RequireTenant(), studentId, input.GuardianId, input.Relationship, input.IsPrimary, input.IsEmergencyContact, input.MayCollect)); await db.SaveChangesAsync(ct); }

    private Task DemandAsync(Guid actor, string permission, CancellationToken ct)
    {
        var featureKey = permission.StartsWith("Admissions.", StringComparison.Ordinal) ? FeatureKeys.Admissions
            : permission.StartsWith("Guardians.", StringComparison.Ordinal) ? FeatureKeys.GuardianManagement
            : FeatureKeys.StudentInformation;
        return access.DemandAsync(actor, permission, featureKey, ct);
    }
    private async Task<IQueryable<Student>> ScopedStudentsAsync(Guid actor, CancellationToken ct)
    {
        var query = db.Students.AsNoTracking();
        if (await permissionService.HasPermissionAsync(actor, Permissions.StudentsManage, ct)) return query;
        return query.Where(student =>
            student.UserId == actor || db.StudentGuardians.Any(link => link.StudentId == student.Id && db.Guardians.Any(guardian => guardian.Id == link.GuardianId && guardian.UserId == actor)) ||
            db.Enrollments.Any(enrollment => enrollment.StudentId == student.Id && enrollment.Status == EnrollmentStatus.Active &&
                db.TeachingAssignments.Any(assignment => assignment.ClassSectionId == enrollment.ClassSectionId && db.StaffProfiles.Any(staff => staff.Id == assignment.StaffId && staff.UserId == actor))));
    }
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
}
