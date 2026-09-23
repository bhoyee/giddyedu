using System.Net.Mail;
using GiddyEdu.BuildingBlocks.Api;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Academics.Domain;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

public sealed record ApplicantGuardianInput(string Name, GuardianRelationshipType Relationship, string Phone, string Email, string Address, bool IsPrimary = false);
public sealed record ApplicantGuardianInfo(Guid Id, string Name, GuardianRelationshipType Relationship, string Phone, string Email, string Address, bool IsPrimary);
public sealed record ApplicantInput(string FirstName, string LastName, DateOnly DateOfBirth, string? Email, string? Phone, string? PreviousSchool, string? Source,
    string? ApplicationNumber = null, string? Gender = null, Guid? ProposedClassLevelId = null, bool SubmitImmediately = false,
    string? PreviousSchoolAddress = null, string? LastClassCompleted = null, DateOnly? LeavingDate = null, string? ReasonForLeaving = null,
    IReadOnlyList<ApplicantGuardianInput>? Guardians = null);
public sealed record ApplicationTransitionInput(ApplicationStatus Status);
public sealed record ApplicantInfo(Guid Id, string ApplicationNumber, string FirstName, string LastName, DateOnly DateOfBirth, ApplicationStatus Status, Guid? StudentId,
    string? Email, string? Phone, string? PreviousSchool, string? Source, DateTimeOffset CreatedAtUtc,
    string? Gender, Guid? ProposedClassLevelId, string? ProposedClassLevelName,
    string? PreviousSchoolAddress, string? LastClassCompleted, DateOnly? LeavingDate, string? ReasonForLeaving,
    IReadOnlyList<ApplicantGuardianInfo> Guardians);
public sealed record ConvertApplicantInput(string AdmissionNumber, Guid AcademicYearId, Guid ClassSectionId, DateOnly EnrolledOn);
public sealed record StudentInfo(Guid Id, string AdmissionNumber, string FirstName, string? MiddleName, string LastName,
    DateOnly DateOfBirth, string? Gender, StudentType StudentType, string? Email, string? Phone,
    StudentStatus Status, DateTimeOffset CreatedAtUtc);
public sealed record StudentDirectoryRow(Guid Id, string AdmissionNumber, string FirstName, string? MiddleName, string LastName,
    StudentType StudentType, StudentStatus Status, DateTimeOffset CreatedAtUtc, string? CurrentClass,
    Guid? GuardianId, string? GuardianName, string? GuardianEmail, string? GuardianPhone);
public sealed record StudentProfileInput(string FirstName, string? MiddleName, string LastName, DateOnly DateOfBirth,
    string? Gender, StudentType StudentType, string? Email = null, string? Phone = null);
public sealed record StudentGuardianRegistrationInput(string FirstName, string LastName, string Phone, string Email,
    string? Gender, string? Address, GuardianRelationshipType Relationship, bool IsPrimary = true,
    bool IsEmergencyContact = true, bool MayCollect = true, Guid? ExistingGuardianId = null);
public sealed record StudentRegistrationInput(string FirstName, string? MiddleName, string LastName, DateOnly DateOfBirth,
    string Gender, StudentType StudentType, string? Email, string? Phone, Guid AcademicYearId, Guid ClassSectionId,
    DateOnly EnrolledOn, string? Address, string? MedicalInformation, string? Allergies,
    string? SpecialEducationalNeeds, string? PrivateNotes, IReadOnlyList<StudentGuardianRegistrationInput>? Guardians,
    bool AutoGenerateStudentId = true, string? CustomStudentId = null);
public sealed record StudentGuardianResult(Guid GuardianId, bool NeedsInvitation);
public sealed record StudentRegistrationResult(Guid StudentId, string AdmissionNumber, IReadOnlyList<StudentGuardianResult> Guardians);
public sealed record GuardianStudentLinkInput(Guid StudentId, GuardianRelationshipType Relationship, bool IsPrimary = false,
    bool IsEmergencyContact = false, bool MayCollect = false);
public sealed record GuardianInput(string FirstName, string LastName, string Phone, string? Email, string? Address = null,
    Guid? StudentId = null, GuardianRelationshipType? Relationship = null, bool IsPrimary = false, bool IsEmergencyContact = false,
    bool MayCollect = false, IReadOnlyList<GuardianStudentLinkInput>? StudentLinks = null);
public sealed record GuardianLinkInput(Guid GuardianId, GuardianRelationshipType Relationship, bool IsPrimary, bool IsEmergencyContact, bool MayCollect);
public sealed record GuardianStatusInput(GuardianStatus Status);
public sealed record GuardianRelationshipInput(GuardianRelationshipType Relationship);
public sealed record GuardianStudentSummary(Guid StudentId, string AdmissionNumber, string FirstName, string? MiddleName, string LastName, string? ClassSectionName, GuardianRelationshipType Relationship);
public sealed record GuardianInfo(Guid Id, string FirstName, string LastName, string Phone, string? Email, string? Address = null,
    bool HasAccount = false, GuardianStatus Status = GuardianStatus.Active, IReadOnlyList<GuardianStudentSummary>? LinkedStudents = null);
public sealed record GuardianStudentSearchResult(Guid Id, string AdmissionNumber, string FirstName, string LastName,
    string ClassLevelName, string ClassSectionName, string CampusName);
public sealed record EnrollmentInfo(Guid Id, Guid AcademicYearId, Guid ClassSectionId, string AcademicYearName,
    string ClassSectionName, DateOnly EnrolledOn, EnrollmentStatus Status);
public sealed record EnrollmentInput(Guid AcademicYearId, Guid ClassSectionId, DateOnly EnrolledOn, EnrollmentStatus PreviousEnrollmentStatus = EnrollmentStatus.Completed);
public sealed record StudentProgressionInput(StudentProgressionType Type, Guid AcademicYearId, Guid ClassSectionId, DateOnly EffectiveOn, string? Reason);
public sealed record StudentDetail(StudentInfo Student, IReadOnlyList<GuardianInfo> Guardians, IReadOnlyList<EnrollmentInfo> Enrollments);
public sealed record StudentPhotoContent(byte[] Content, string ContentType);
public sealed record ApplicantSensitiveInput(string? Address, string? MedicalInformation, string? Allergies, string? SpecialEducationalNeeds);
public sealed record ApplicantSensitiveInfo(string? Address, string? MedicalInformation, string? Allergies, string? SpecialEducationalNeeds, DateTimeOffset UpdatedAtUtc);
public sealed record StudentSensitiveInput(string? Address, string? MedicalInformation, string? Allergies, string? SpecialEducationalNeeds,
    string? Genotype, string? BloodGroup, decimal? WeightKg, decimal? HeightCm, string? Disability, string? PrivateNotes);
public sealed record StudentSensitiveInfo(string? Address, string? MedicalInformation, string? Allergies, string? SpecialEducationalNeeds,
    string? Genotype, string? BloodGroup, decimal? WeightKg, decimal? HeightCm, string? Disability, string? PrivateNotes, DateTimeOffset UpdatedAtUtc);
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
    Task<PageResult<StudentDirectoryRow>> ListStudentsAsync(Guid actor, int page, int pageSize, string? search, Guid? classLevelId = null, Guid? classSectionId = null, CancellationToken ct = default);
    Task<StudentRegistrationResult> CreateStudentAsync(Guid actor, StudentRegistrationInput input, CancellationToken ct = default);
    Task<StudentDetail> GetStudentAsync(Guid actor, Guid studentId, CancellationToken ct = default);
    Task<StudentPhotoContent?> GetStudentPhotoAsync(Guid actor, Guid studentId, CancellationToken ct = default);
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
    Task ChangeGuardianStatusAsync(Guid actor, Guid guardianId, GuardianStatus status, CancellationToken ct = default);
    Task ChangeGuardianRelationshipAsync(Guid actor, Guid guardianId, Guid studentId, GuardianRelationshipType relationship, CancellationToken ct = default);
    Task<PageResult<GuardianInfo>> ListGuardiansAsync(Guid actor, int page, int pageSize, string? search, CancellationToken ct = default,
        string? sort = null, bool descending = false, Guid? classLevelId = null, Guid? classSectionId = null);
    Task<IReadOnlyList<GuardianStudentSearchResult>> SearchGuardianStudentsAsync(Guid actor, string query, CancellationToken ct = default);
    Task LinkGuardianAsync(Guid actor, Guid studentId, GuardianLinkInput input, CancellationToken ct = default);
}

public sealed class StudentLifecycleService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IPermissionService permissionService, IFileObjectStorage storage, IClock clock) : IStudentLifecycleService
{
    public async Task<StudentRegistrationResult> CreateStudentAsync(Guid actor, StudentRegistrationInput input, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StudentsManage, ct);
        var tenantId = RequireTenant();
        var campusId = tenant.CampusId ?? throw new InvalidOperationException("Select an active campus before adding a student.");
        var hasSensitiveDetails = new[] { input.Address, input.MedicalInformation, input.Allergies, input.SpecialEducationalNeeds, input.PrivateNotes }.Any(x => !string.IsNullOrWhiteSpace(x));
        if (hasSensitiveDetails) await access.DemandAsync(actor, Permissions.StudentsSensitiveManage, FeatureKeys.StudentInformation, ct);
        var guardianInputs = input.Guardians ?? [];
        if (guardianInputs.Count > 0) await access.DemandAsync(actor, Permissions.GuardiansManage, FeatureKeys.GuardianManagement, ct);
        if (guardianInputs.Count(x => x.IsPrimary) > 1) throw new InvalidOperationException("Only one guardian can be marked as the primary guardian.");
        if (input.DateOfBirth >= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)) throw new ArgumentException("Date of birth must be in the past.");
        if (string.IsNullOrWhiteSpace(input.Gender) || input.Gender.Trim().Length > 30) throw new ArgumentException("Select the student's gender.");
        if (!string.IsNullOrWhiteSpace(input.Phone) && (input.Phone.Length != 11 || input.Phone.Any(x => !char.IsDigit(x)))) throw new ArgumentException("Student phone must contain exactly 11 digits.");
        if (!string.IsNullOrWhiteSpace(input.Email) && !MailAddress.TryCreate(input.Email, out _)) throw new ArgumentException("Enter a valid student email address.");
        if (!await db.ClassSections.AnyAsync(x => x.Id == input.ClassSectionId && x.AcademicYearId == input.AcademicYearId
                && x.CampusId == campusId && x.IsActive, ct))
            throw new InvalidOperationException("Select an active class in the current campus and academic year.");
        if (!string.IsNullOrWhiteSpace(input.Email) && await db.Students.AnyAsync(x => x.Email != null && x.Email.ToLower() == input.Email.Trim().ToLower(), ct))
            throw new InvalidOperationException("A student with this email already exists in this school.");
        var duplicateStudent = await db.Students.AsNoTracking().FirstOrDefaultAsync(x =>
            x.FirstName.ToLower() == input.FirstName.Trim().ToLower()
            && x.LastName.ToLower() == input.LastName.Trim().ToLower()
            && x.DateOfBirth == input.DateOfBirth
            && x.Gender != null && x.Gender.ToLower() == input.Gender.Trim().ToLower(), ct);
        if (duplicateStudent is not null)
            throw new InvalidOperationException($"A student with the same name, date of birth and gender already exists ({duplicateStudent.AdmissionNumber}). Open the existing record instead of creating a duplicate.");

        var admissionNumber = input.AutoGenerateStudentId
            ? await GenerateAdmissionNumberAsync(ct)
            : NormalizeCustomStudentId(input.CustomStudentId);
        if (!input.AutoGenerateStudentId && await db.Students.AnyAsync(x => x.AdmissionNumber == admissionNumber, ct))
            throw new InvalidOperationException("This student ID is already assigned to another student in this school.");

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var studentId = Guid.NewGuid();
        var student = new Student(studentId, tenantId, admissionNumber, input.FirstName, input.LastName, input.DateOfBirth, null, clock.UtcNow, input.Email);
        student.CompleteRegistration(input.MiddleName, input.Gender, input.StudentType, input.Phone);
        db.Students.Add(student);
        db.Enrollments.Add(new Enrollment(Guid.NewGuid(), tenantId, studentId, input.AcademicYearId, input.ClassSectionId, input.EnrolledOn, clock.UtcNow));
        if (hasSensitiveDetails)
            db.StudentSensitiveRecords.Add(new StudentSensitiveRecord(tenantId, studentId, input.Address, input.MedicalInformation,
                input.Allergies, input.SpecialEducationalNeeds, null, null, null, null, null, input.PrivateNotes, clock.UtcNow));

        var guardianResults = new List<StudentGuardianResult>();
        var linkedGuardianIds = new HashSet<Guid>();
        foreach (var guardianInput in guardianInputs)
        {
            Guardian? guardian; var guardianWasCreated = false;
            if (guardianInput.ExistingGuardianId.HasValue)
            {
                guardian = await db.Guardians.SingleOrDefaultAsync(x => x.Id == guardianInput.ExistingGuardianId.Value && x.DeletedAtUtc == null, ct)
                    ?? throw new KeyNotFoundException("The selected guardian was not found in this school.");
            }
            else
            {
                if (guardianInput.Phone.Length != 11 || guardianInput.Phone.Any(x => !char.IsDigit(x))) throw new ArgumentException("Guardian phone must contain exactly 11 digits.");
                if (!MailAddress.TryCreate(guardianInput.Email, out _)) throw new ArgumentException("Enter a valid guardian email address.");
                var phone = guardianInput.Phone.Trim(); var email = guardianInput.Email.Trim().ToLowerInvariant();
                var matches = await db.Guardians.Where(x => x.DeletedAtUtc == null && (x.Phone == phone || x.Email != null && x.Email.ToLower() == email)).ToListAsync(ct);
                if (matches.Select(x => x.Id).Distinct().Count() > 1) throw new InvalidOperationException("The guardian email and phone belong to different existing records. Select the correct guardian record.");
                guardian = matches.SingleOrDefault();
                if (guardian is null)
                {
                    guardian = new Guardian(Guid.NewGuid(), tenantId, guardianInput.FirstName, guardianInput.LastName, phone, email, clock.UtcNow);
                    guardian.Update(guardianInput.FirstName, guardianInput.LastName, phone, email, guardianInput.Address, guardianInput.Gender);
                    db.Guardians.Add(guardian);
                    guardianWasCreated = true;
                }
                else if (!string.Equals(guardian.FirstName, guardianInput.FirstName, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(guardian.LastName, guardianInput.LastName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("These guardian contact details already exist. Search for and select the existing guardian record.");
            }
            if (!linkedGuardianIds.Add(guardian.Id)) continue;
            guardianResults.Add(new StudentGuardianResult(guardian.Id, guardianWasCreated));
            db.StudentGuardians.Add(new StudentGuardian(tenantId, studentId, guardian.Id, guardianInput.Relationship,
                guardianInput.IsPrimary, guardianInput.IsEmergencyContact, guardianInput.MayCollect));
        }
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(studentId, admissionNumber, guardianResults);
    }

    private static string NormalizeCustomStudentId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Enter the school's existing student ID or use automatic generation.");
        var result = value.Trim().ToUpperInvariant();
        if (result.Length > 50) throw new ArgumentException("Student ID must not exceed 50 characters.");
        if (result.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '/' and not '_'))
            throw new ArgumentException("Student ID may contain letters, numbers, hyphens, slashes and underscores only.");
        return result;
    }

    private async Task<string> GenerateAdmissionNumberAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = $"STU-{clock.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            if (!await db.Students.AnyAsync(x => x.AdmissionNumber == candidate, ct)) return candidate;
        }
        throw new InvalidOperationException("A unique student number could not be generated. Try again.");
    }
    public async Task<Guid> CreateApplicantAsync(Guid actor, ApplicantInput input, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.AdmissionsManage, ct);
        var guardians = input.Guardians ?? [];
        if (guardians.Count == 0) throw new ArgumentException("At least one guardian is required.");
        foreach (var guardian in guardians)
        {
            if (string.IsNullOrWhiteSpace(guardian.Name)) throw new ArgumentException("Guardian name is required.");
            if (guardian.Phone.Length != 11 || guardian.Phone.Any(x => !char.IsDigit(x))) throw new ArgumentException("Guardian phone must contain exactly 11 digits.");
            if (!MailAddress.TryCreate(guardian.Email, out _)) throw new ArgumentException("Enter a valid guardian email address.");
            if (string.IsNullOrWhiteSpace(guardian.Address)) throw new ArgumentException("Guardian address is required.");
        }
        if (guardians.Count(x => x.IsPrimary) > 1) throw new InvalidOperationException("Only one guardian can be marked as the primary guardian.");

        var tenantId = RequireTenant();
        var id = Guid.NewGuid();
        var applicationNumber = string.IsNullOrWhiteSpace(input.ApplicationNumber) ? $"APP-{clock.UtcNow:yyyy}-{id.ToString("N")[..10].ToUpperInvariant()}" : input.ApplicationNumber;
        if (input.ProposedClassLevelId.HasValue && !await db.ClassLevels.AsNoTracking().AnyAsync(x => x.Id == input.ProposedClassLevelId.Value, ct))
            throw new InvalidOperationException("The proposed class was not found.");
        var applicant = new Applicant(id, tenantId, applicationNumber, input.FirstName, input.LastName, input.DateOfBirth, input.Email, input.Phone, input.PreviousSchool, input.Source, clock.UtcNow,
            input.Gender, input.ProposedClassLevelId, input.PreviousSchoolAddress, input.LastClassCompleted, input.LeavingDate, input.ReasonForLeaving);
        if (input.SubmitImmediately) applicant.Transition(ApplicationStatus.Submitted, clock.UtcNow);
        db.Applicants.Add(applicant);
        foreach (var guardian in guardians)
            db.ApplicantGuardians.Add(new ApplicantGuardian(Guid.NewGuid(), tenantId, id, guardian.Name, guardian.Relationship, guardian.Phone.Trim(), guardian.Email.Trim(), guardian.Address, guardian.IsPrimary, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task<PageResult<ApplicantInfo>> ListApplicantsAsync(Guid actor, int page, int pageSize, ApplicationStatus? status, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.AdmissionsView, ct); RequireTenant(); page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = db.Applicants.AsNoTracking().Where(x => !status.HasValue || x.Status == status);
        var total = await q.LongCountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var levelIds = rows.Where(x => x.ProposedClassLevelId.HasValue).Select(x => x.ProposedClassLevelId!.Value).Distinct().ToArray();
        var levels = await db.ClassLevels.AsNoTracking().Where(x => levelIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var applicantIds = rows.Select(x => x.Id).ToArray();
        var guardians = await db.ApplicantGuardians.AsNoTracking().Where(x => applicantIds.Contains(x.ApplicantId))
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Name)
            .Select(x => new { x.ApplicantId, Info = new ApplicantGuardianInfo(x.Id, x.Name, x.Relationship, x.Phone, x.Email, x.Address, x.IsPrimary) })
            .ToListAsync(ct);
        var guardiansByApplicant = guardians.GroupBy(x => x.ApplicantId).ToDictionary(x => x.Key, x => (IReadOnlyList<ApplicantGuardianInfo>)x.Select(item => item.Info).ToList());
        var items = rows.Select(x => new ApplicantInfo(x.Id, x.ApplicationNumber, x.FirstName, x.LastName, x.DateOfBirth, x.Status, x.StudentId,
            x.Email, x.Phone, x.PreviousSchool, x.Source, x.CreatedAtUtc,
            x.Gender, x.ProposedClassLevelId, x.ProposedClassLevelId.HasValue ? levels.GetValueOrDefault(x.ProposedClassLevelId.Value) : null,
            x.PreviousSchoolAddress, x.LastClassCompleted, x.LeavingDate, x.ReasonForLeaving,
            guardiansByApplicant.GetValueOrDefault(x.Id, []))).ToList();
        return new(items, page, pageSize, total);
    }

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
        var id = Guid.NewGuid(); db.Students.Add(new Student(id, RequireTenant(), input.AdmissionNumber, applicant.FirstName, applicant.LastName, applicant.DateOfBirth, applicant.Id, clock.UtcNow, applicant.Email)); db.Enrollments.Add(new Enrollment(Guid.NewGuid(), RequireTenant(), id, input.AcademicYearId, input.ClassSectionId, input.EnrolledOn, clock.UtcNow)); var sensitive = await db.ApplicantSensitiveRecords.AsNoTracking().SingleOrDefaultAsync(x => x.ApplicantId == applicantId, ct); if (sensitive is not null) db.StudentSensitiveRecords.Add(new StudentSensitiveRecord(RequireTenant(), id, sensitive.Address, sensitive.MedicalInformation, sensitive.Allergies, sensitive.SpecialEducationalNeeds, null, null, null, null, null, null, clock.UtcNow)); applicant.MarkConverted(id, clock.UtcNow); await db.SaveChangesAsync(ct); return id;
    }

    public async Task<PageResult<StudentDirectoryRow>> ListStudentsAsync(Guid actor, int page, int pageSize, string? search, Guid? classLevelId = null, Guid? classSectionId = null, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StudentsView, ct); RequireTenant(); page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var canViewGuardians = await permissionService.HasPermissionAsync(actor, Permissions.GuardiansView, ct);
        var query = await ScopedStudentsAsync(actor, ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.AdmissionNumber.ToLower().Contains(term)
                || x.FirstName.ToLower().Contains(term)
                || x.LastName.ToLower().Contains(term)
                || x.MiddleName != null && x.MiddleName.ToLower().Contains(term));
        }
        if (classLevelId.HasValue || classSectionId.HasValue)
        {
            var matchingStudentIds = await (from enrollment in db.Enrollments.AsNoTracking()
                                            join section in db.ClassSections.AsNoTracking() on enrollment.ClassSectionId equals section.Id
                                            where enrollment.Status == EnrollmentStatus.Active
                                                && (!classSectionId.HasValue || enrollment.ClassSectionId == classSectionId.Value)
                                                && (!classLevelId.HasValue || section.ClassLevelId == classLevelId.Value)
                                            select enrollment.StudentId).Distinct().ToListAsync(ct);
            query = query.Where(x => matchingStudentIds.Contains(x.Id));
        }
        var total = await query.LongCountAsync(ct);
        var students = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.AdmissionNumber, x.FirstName, x.MiddleName, x.LastName, x.StudentType, x.Status, x.CreatedAtUtc }).ToListAsync(ct);
        var ids = students.Select(x => x.Id).ToArray();
        var classes = await (from enrollment in db.Enrollments.AsNoTracking()
                             join section in db.ClassSections.AsNoTracking() on enrollment.ClassSectionId equals section.Id
                             where ids.Contains(enrollment.StudentId) && enrollment.Status == EnrollmentStatus.Active
                             select new { enrollment.StudentId, section.Name }).ToDictionaryAsync(x => x.StudentId, x => x.Name, ct);
        var guardians = canViewGuardians
            ? await (from link in db.StudentGuardians.AsNoTracking()
                     join guardian in db.Guardians.AsNoTracking() on link.GuardianId equals guardian.Id
                     where ids.Contains(link.StudentId)
                     orderby link.IsPrimary descending, guardian.LastName, guardian.FirstName
                     select new { link.StudentId, guardian.Id, guardian.FirstName, guardian.LastName, guardian.Email, guardian.Phone })
                .ToListAsync(ct)
            : [];
        var primaryGuardians = guardians.GroupBy(x => x.StudentId).ToDictionary(x => x.Key, x => x.First());
        var rows = students.Select(student => { primaryGuardians.TryGetValue(student.Id, out var guardian); return new StudentDirectoryRow(
            student.Id, student.AdmissionNumber, student.FirstName, student.MiddleName, student.LastName, student.StudentType,
            student.Status, student.CreatedAtUtc, classes.GetValueOrDefault(student.Id), guardian?.Id,
            guardian is null ? null : $"{guardian.FirstName} {guardian.LastName}", guardian?.Email, guardian?.Phone); }).ToList();
        return new(rows, page, pageSize, total);
    }

    public async Task<StudentDetail> GetStudentAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StudentsView, ct); RequireTenant(); var canManage = await permissionService.HasPermissionAsync(actor, Permissions.StudentsManage, ct); var student = await (await ScopedStudentsAsync(actor, ct)).Where(x => x.Id == studentId).Select(x => new StudentInfo(x.Id, x.AdmissionNumber, x.FirstName, x.MiddleName, x.LastName, x.DateOfBirth, x.Gender, x.StudentType, x.Email, x.Phone, x.Status, x.CreatedAtUtc)).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Student was not found.");
        var guardians = await (from link in db.StudentGuardians join guardian in db.Guardians on link.GuardianId equals guardian.Id where link.StudentId == studentId && (canManage || guardian.UserId == actor) select new GuardianInfo(guardian.Id, guardian.FirstName, guardian.LastName, guardian.Phone, guardian.Email)).ToListAsync(ct);
        var enrollments = await (from enrollment in db.Enrollments.AsNoTracking()
                                 join year in db.AcademicYears.AsNoTracking() on enrollment.AcademicYearId equals year.Id
                                 join section in db.ClassSections.AsNoTracking() on enrollment.ClassSectionId equals section.Id
                                 where enrollment.StudentId == studentId
                                 orderby enrollment.EnrolledOn descending
                                 select new EnrollmentInfo(enrollment.Id, enrollment.AcademicYearId, enrollment.ClassSectionId,
                                     year.Name, section.Name, enrollment.EnrolledOn, enrollment.Status)).ToListAsync(ct);
        return new(student, guardians, enrollments);
    }

    public async Task<StudentPhotoContent?> GetStudentPhotoAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StudentsView, ct);
        if (!await (await ScopedStudentsAsync(actor, ct)).AnyAsync(x => x.Id == studentId, ct))
            throw new KeyNotFoundException("Student was not found.");

        var photo = await db.StoredFiles.AsNoTracking()
            .Where(x => x.EntityType == "Student" && x.EntityId == studentId && x.Category == "photo" && x.Status == StoredFileStatus.Available)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.ObjectKey, x.ContentType })
            .FirstOrDefaultAsync(ct);
        if (photo is null) return null;
        return new StudentPhotoContent(await storage.ReadBytesAsync(photo.ObjectKey, 10 * 1024 * 1024, ct), photo.ContentType);
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
    { await DemandAsync(actor, Permissions.StudentsManage, ct); var student = await db.Students.SingleOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new KeyNotFoundException("Student was not found."); student.UpdatePersonalInformation(input.FirstName, input.MiddleName, input.LastName, input.DateOfBirth, input.Gender, input.StudentType, input.Email, input.Phone); await db.SaveChangesAsync(ct); }

    public async Task WithdrawStudentAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StudentsManage, ct); var student = await db.Students.SingleOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new KeyNotFoundException("Student was not found."); student.Withdraw(); var active = await db.Enrollments.Where(x => x.StudentId == studentId && x.Status == EnrollmentStatus.Active).ToListAsync(ct); foreach (var enrollment in active) enrollment.Complete(EnrollmentStatus.Withdrawn); await db.SaveChangesAsync(ct); }

    public async Task<StudentSensitiveInfo?> GetStudentSensitiveAsync(Guid actor, Guid studentId, CancellationToken ct = default)
    { await access.DemandAsync(actor, Permissions.StudentsSensitiveView, FeatureKeys.StudentInformation, ct); if (!await (await ScopedStudentsAsync(actor, ct)).AnyAsync(x => x.Id == studentId, ct)) throw new KeyNotFoundException("Student was not found."); return await db.StudentSensitiveRecords.AsNoTracking().Where(x => x.StudentId == studentId).Select(x => new StudentSensitiveInfo(x.Address, x.MedicalInformation, x.Allergies, x.SpecialEducationalNeeds, x.Genotype, x.BloodGroup, x.WeightKg, x.HeightCm, x.Disability, x.PrivateNotes, x.UpdatedAtUtc)).SingleOrDefaultAsync(ct); }

    public async Task UpsertStudentSensitiveAsync(Guid actor, Guid studentId, StudentSensitiveInput input, CancellationToken ct = default)
    { await access.DemandAsync(actor, Permissions.StudentsSensitiveManage, FeatureKeys.StudentInformation, ct); if (!await (await ScopedStudentsAsync(actor, ct)).AnyAsync(x => x.Id == studentId, ct)) throw new KeyNotFoundException("Student was not found."); var record = await db.StudentSensitiveRecords.SingleOrDefaultAsync(x => x.StudentId == studentId, ct); if (record is null) db.StudentSensitiveRecords.Add(new StudentSensitiveRecord(RequireTenant(), studentId, input.Address, input.MedicalInformation, input.Allergies, input.SpecialEducationalNeeds, input.Genotype, input.BloodGroup, input.WeightKg, input.HeightCm, input.Disability, input.PrivateNotes, clock.UtcNow)); else record.Update(input.Address, input.MedicalInformation, input.Allergies, input.SpecialEducationalNeeds, input.Genotype, input.BloodGroup, input.WeightKg, input.HeightCm, input.Disability, input.PrivateNotes, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<Guid> CreateGuardianAsync(Guid actor, GuardianInput input, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.GuardiansManage, ct);
        var (phone, email) = ValidateGuardianContact(input.Phone, input.Email);
        if (string.IsNullOrWhiteSpace(input.Address)) throw new ArgumentException("Guardian address is required.", nameof(input));
        if (await db.Guardians.AnyAsync(x => x.Phone == phone || x.Email != null && x.Email.ToLower() == email, ct))
            throw new InvalidOperationException("A guardian with this phone number or email already exists. Open that guardian's record instead of creating a duplicate.");
        if (input.StudentId.HasValue != input.Relationship.HasValue) throw new ArgumentException("Select both a student and the guardian's relationship to that student.");
        var links = input.StudentLinks?.ToArray() ?? (input.StudentId is Guid legacyStudentId
            ? [new GuardianStudentLinkInput(legacyStudentId, input.Relationship!.Value, input.IsPrimary, input.IsEmergencyContact, input.MayCollect)] : []);
        if (links.Length == 0) throw new ArgumentException("Link the guardian to at least one student.", nameof(input));
        if (links.Length > 20) throw new ArgumentException("A guardian can be linked to at most 20 students in one request.", nameof(input));
        if (links.Select(x => x.StudentId).Distinct().Count() != links.Length) throw new ArgumentException("The same student cannot be linked more than once.", nameof(input));
        foreach (var link in links) await ValidateGuardianStudentAsync(link.StudentId, link.Relationship, link.IsPrimary, ct);
        var id = Guid.NewGuid();
        var guardian = new Guardian(id, RequireTenant(), input.FirstName, input.LastName, phone, email, clock.UtcNow);
        guardian.Update(input.FirstName, input.LastName, phone, email, input.Address);
        db.Guardians.Add(guardian);
        foreach (var link in links)
            db.StudentGuardians.Add(new StudentGuardian(RequireTenant(), link.StudentId, id, link.Relationship,
                link.IsPrimary, link.IsEmergencyContact, link.MayCollect));
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task<GuardianInfo> GetGuardianAsync(Guid actor, Guid guardianId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.GuardiansView, ct); var query = db.Guardians.AsNoTracking().Where(x => x.Id == guardianId); if (!await permissionService.HasPermissionAsync(actor, Permissions.GuardiansManage, ct)) query = query.Where(x => x.UserId == actor); return await query.Select(x => new GuardianInfo(x.Id, x.FirstName, x.LastName, x.Phone, x.Email, x.Address, x.UserId != null, x.Status, null)).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Guardian was not found."); }

    public async Task UpdateGuardianAsync(Guid actor, Guid guardianId, GuardianInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.GuardiansManage, ct); var guardian = await db.Guardians.SingleOrDefaultAsync(x => x.Id == guardianId, ct) ?? throw new KeyNotFoundException("Guardian was not found."); var (phone, email) = ValidateGuardianContact(input.Phone, input.Email); if (string.IsNullOrWhiteSpace(input.Address)) throw new ArgumentException("Guardian address is required.", nameof(input)); if (await db.Guardians.AnyAsync(x => x.Id != guardianId && (x.Phone == phone || x.Email != null && x.Email.ToLower() == email), ct)) throw new InvalidOperationException("A guardian with this phone number or email already exists."); guardian.Update(input.FirstName, input.LastName, phone, email, input.Address); await db.SaveChangesAsync(ct); }

    public async Task ChangeGuardianStatusAsync(Guid actor, Guid guardianId, GuardianStatus status, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.GuardiansManage, ct); var guardian = await db.Guardians.SingleOrDefaultAsync(x => x.Id == guardianId, ct) ?? throw new KeyNotFoundException("Guardian was not found."); guardian.ChangeStatus(status); await db.SaveChangesAsync(ct); }

    public async Task ChangeGuardianRelationshipAsync(Guid actor, Guid guardianId, Guid studentId, GuardianRelationshipType relationship, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.GuardiansManage, ct); var link = await db.StudentGuardians.SingleOrDefaultAsync(x => x.GuardianId == guardianId && x.StudentId == studentId, ct) ?? throw new KeyNotFoundException("The guardian is not linked to this student."); link.ChangeRelationship(relationship); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<GuardianStudentSearchResult>> SearchGuardianStudentsAsync(Guid actor, string query, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.GuardiansManage, ct);
        await access.DemandAsync(actor, Permissions.StudentsView, FeatureKeys.StudentInformation, ct);
        var campusId = tenant.CampusId ?? throw new InvalidOperationException("Select an active campus before searching for students.");
        var term = query?.Trim() ?? "";
        if (term.Length < 2) return [];
        if (term.Length > 100) throw new ArgumentException("Search text must not exceed 100 characters.", nameof(query));
        var search = term.ToLowerInvariant();
        return await (from enrollment in db.Enrollments.AsNoTracking()
                      join student in db.Students.AsNoTracking() on enrollment.StudentId equals student.Id
                      join section in db.ClassSections.AsNoTracking() on enrollment.ClassSectionId equals section.Id
                      join level in db.ClassLevels.AsNoTracking() on section.ClassLevelId equals level.Id
                      join campus in db.Campuses.AsNoTracking() on section.CampusId equals campus.Id
                      where enrollment.Status == EnrollmentStatus.Active && student.Status == StudentStatus.Active && section.CampusId == campusId
                          && (student.FirstName.ToLower().Contains(search) || student.LastName.ToLower().Contains(search) || student.AdmissionNumber.ToLower().Contains(search))
                      orderby student.LastName, student.FirstName
                      select new GuardianStudentSearchResult(student.Id, student.AdmissionNumber, student.FirstName, student.LastName,
                          level.Name, section.Name, campus.Name)).Take(20).ToListAsync(ct);
    }

    public async Task<PageResult<GuardianInfo>> ListGuardiansAsync(Guid actor, int page, int pageSize, string? search, CancellationToken ct = default,
        string? sort = null, bool descending = false, Guid? classLevelId = null, Guid? classSectionId = null)
    {
        await DemandAsync(actor, Permissions.GuardiansView, ct);
        RequireTenant();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Guardians.AsNoTracking();
        if (!await permissionService.HasPermissionAsync(actor, Permissions.GuardiansManage, ct)) query = query.Where(x => x.UserId == actor);
        if (classSectionId.HasValue)
            query = query.Where(guardian => db.StudentGuardians.Any(link => link.GuardianId == guardian.Id
                && db.Enrollments.Any(enrollment => enrollment.StudentId == link.StudentId
                    && enrollment.Status == EnrollmentStatus.Active && enrollment.ClassSectionId == classSectionId.Value)));
        else if (classLevelId.HasValue)
            query = query.Where(guardian => db.StudentGuardians.Any(link => link.GuardianId == guardian.Id
                && db.Enrollments.Any(enrollment => enrollment.StudentId == link.StudentId && enrollment.Status == EnrollmentStatus.Active
                    && db.ClassSections.Any(section => section.Id == enrollment.ClassSectionId && section.ClassLevelId == classLevelId.Value))));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            if (term.Length > 100) throw new ArgumentException("Search text must not exceed 100 characters.", nameof(search));
            query = query.Where(x => x.FirstName.ToLower().Contains(term) || x.LastName.ToLower().Contains(term)
                || x.Phone.Contains(term) || x.Email != null && x.Email.ToLower().Contains(term));
        }
        var total = await query.LongCountAsync(ct);
        query = (sort?.ToLowerInvariant(), descending) switch
        {
            ("firstname", false) => query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ThenBy(x => x.Id),
            ("firstname", true) => query.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName).ThenBy(x => x.Id),
            ("phone", false) => query.OrderBy(x => x.Phone).ThenBy(x => x.Id),
            ("phone", true) => query.OrderByDescending(x => x.Phone).ThenBy(x => x.Id),
            ("email", false) => query.OrderBy(x => x.Email).ThenBy(x => x.Id),
            ("email", true) => query.OrderByDescending(x => x.Email).ThenBy(x => x.Id),
            (_, true) => query.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName).ThenBy(x => x.Id),
            _ => query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id)
        };
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new GuardianInfo(x.Id, x.FirstName, x.LastName, x.Phone, x.Email, null, x.UserId != null, x.Status, null)).ToListAsync(ct);
        var guardianIds = items.Select(x => x.Id).ToArray();
        var studentLinks = await (from link in db.StudentGuardians.AsNoTracking()
                                  join student in db.Students.AsNoTracking() on link.StudentId equals student.Id
                                  where guardianIds.Contains(link.GuardianId)
                                  let className = (from enrollment in db.Enrollments.AsNoTracking()
                                                   join section in db.ClassSections.AsNoTracking() on enrollment.ClassSectionId equals section.Id
                                                   where enrollment.StudentId == student.Id && enrollment.Status == EnrollmentStatus.Active
                                                   orderby enrollment.CreatedAtUtc descending select section.Name).FirstOrDefault()
                                  select new { link.GuardianId, Summary = new GuardianStudentSummary(student.Id, student.AdmissionNumber, student.FirstName, student.MiddleName, student.LastName, className, link.Relationship) }).ToListAsync(ct);
        var linksByGuardian = studentLinks.GroupBy(x => x.GuardianId).ToDictionary(x => x.Key, x => (IReadOnlyList<GuardianStudentSummary>)x.Select(y => y.Summary).ToList());
        items = items.Select(x => x with { LinkedStudents = linksByGuardian.GetValueOrDefault(x.Id, []) }).ToList();
        return new(items, page, pageSize, total);
    }

    public async Task LinkGuardianAsync(Guid actor, Guid studentId, GuardianLinkInput input, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.GuardiansManage, ct); if (!await db.Students.AnyAsync(x => x.Id == studentId, ct) || !await db.Guardians.AnyAsync(x => x.Id == input.GuardianId, ct)) throw new InvalidOperationException("Student and guardian must belong to the current tenant."); if (input.IsPrimary && await db.StudentGuardians.AnyAsync(x => x.StudentId == studentId && x.IsPrimary, ct)) throw new InvalidOperationException("A student can have only one primary guardian."); if (await db.StudentGuardians.AnyAsync(x => x.StudentId == studentId && x.GuardianId == input.GuardianId, ct)) return; db.StudentGuardians.Add(new StudentGuardian(RequireTenant(), studentId, input.GuardianId, input.Relationship, input.IsPrimary, input.IsEmergencyContact, input.MayCollect)); await db.SaveChangesAsync(ct); }

    private static (string Phone, string Email) ValidateGuardianContact(string phone, string? email)
    {
        var normalizedPhone = phone?.Trim() ?? "";
        if (normalizedPhone.Length != 11 || normalizedPhone.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Phone number must contain exactly 11 digits.", nameof(phone));
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? "";
        if (normalizedEmail.Length > 320 || !MailAddress.TryCreate(normalizedEmail, out var parsed) || parsed.Address != normalizedEmail)
            throw new ArgumentException("A valid email address is required.", nameof(email));
        return (normalizedPhone, normalizedEmail);
    }

    private async Task ValidateGuardianStudentAsync(Guid studentId, GuardianRelationshipType relationship, bool isPrimary, CancellationToken ct)
    {
        if (!Enum.IsDefined(relationship)) throw new ArgumentException("Choose a valid relationship to the student.", nameof(relationship));
        var campusId = tenant.CampusId ?? throw new InvalidOperationException("Select an active campus before linking a student.");
        var inCampus = await (from enrollment in db.Enrollments
                              join section in db.ClassSections on enrollment.ClassSectionId equals section.Id
                              join student in db.Students on enrollment.StudentId equals student.Id
                              where student.Id == studentId && student.Status == StudentStatus.Active && enrollment.Status == EnrollmentStatus.Active && section.CampusId == campusId
                              select student.Id).AnyAsync(ct);
        if (!inCampus) throw new InvalidOperationException("Select an active student in the current campus.");
        if (isPrimary && await db.StudentGuardians.AnyAsync(x => x.StudentId == studentId && x.IsPrimary, ct))
            throw new InvalidOperationException("This student already has a primary guardian.");
    }

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
