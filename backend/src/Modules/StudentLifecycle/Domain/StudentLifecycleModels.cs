using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.StudentLifecycle.Domain;

public enum ImportOperationStatus { AwaitingUpload, Queued, Processing, Completed, Failed }

public sealed class ImportOperation : GiddyEdu.BuildingBlocks.Tenancy.ITenantOwned
{
    private ImportOperation() { }
    public ImportOperation(Guid id, Guid tenantId, string importType, Guid requestedByUserId, DateTimeOffset now, Guid? campusId = null)
    { if (id == Guid.Empty || tenantId == Guid.Empty || requestedByUserId == Guid.Empty) throw new ArgumentException("Import identifiers are required."); Id = id; TenantId = tenantId; CampusId = campusId; ImportType = Required(importType, 50); RequestedByUserId = requestedByUserId; Status = ImportOperationStatus.AwaitingUpload; CreatedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? CampusId { get; private set; }
    public int? StaffCategory { get; private set; }
    public string ImportType { get; private set; } = null!;
    public Guid RequestedByUserId { get; private set; }
    public Guid? SourceFileId { get; private set; }
    public Guid? ErrorFileId { get; private set; }
    public ImportOperationStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public int ImportedRows { get; private set; }
    public int RejectedRows { get; private set; }
    public string? ErrorSummary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public void Queue(Guid sourceFileId) { if (Status != ImportOperationStatus.AwaitingUpload || sourceFileId == Guid.Empty) throw new InvalidOperationException("Only a completed import upload can be queued."); SourceFileId = sourceFileId; Status = ImportOperationStatus.Queued; }
    public void SetStaffCategory(int category) { if (ImportType != "Staff" || Status != ImportOperationStatus.AwaitingUpload || category is < 0 or > 2) throw new ArgumentException("Select a valid staff category."); StaffCategory = category; }
    public void Start(DateTimeOffset now) { if (Status != ImportOperationStatus.Queued) throw new InvalidOperationException("Only queued imports can start."); Status = ImportOperationStatus.Processing; StartedAtUtc = now; ErrorSummary = null; }
    public void Complete(int totalRows, int importedRows, DateTimeOffset now, int rejectedRows = 0, Guid? errorFileId = null) { if (Status != ImportOperationStatus.Processing) throw new InvalidOperationException("Only processing imports can complete."); TotalRows = totalRows; ImportedRows = importedRows; RejectedRows = rejectedRows; ErrorFileId = errorFileId; Status = ImportOperationStatus.Completed; CompletedAtUtc = now; }
    public void Fail(int totalRows, int rejectedRows, string error, DateTimeOffset now, Guid? errorFileId = null) { TotalRows = Math.Max(0, totalRows); RejectedRows = Math.Max(0, rejectedRows); ErrorSummary = Required(error, 2000); ErrorFileId = errorFileId; Status = ImportOperationStatus.Failed; CompletedAtUtc = now; }
    private static string Required(string value, int max) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required."); var result = value.Trim(); if (result.Length > max) throw new ArgumentException($"Value exceeds {max} characters."); return result; }
}

public enum ApplicationStatus { Draft, Submitted, UnderReview, Waitlisted, Offered, Accepted, Rejected, Withdrawn, Converted }
public enum StudentStatus { Active, Suspended, Withdrawn, Graduated, Alumni }
public enum GuardianRelationshipType { Mother, Father, Parent, LegalGuardian, Relative, Sponsor, Other, Stepmother, Stepfather, Grandmother, Grandfather, Aunt, Uncle, Sibling, FosterParent, AdoptiveParent, Caregiver }
public enum EnrollmentStatus { Active, Completed, Withdrawn, Transferred }
public enum StudentProgressionType { Promotion, RepeatClass, Transfer }
public enum InterviewStatus { Scheduled, Completed, Cancelled, NoShow }
public enum AdmissionResponse { Pending, Accepted, Declined }

public sealed class Applicant : ITenantOwned
{
    private Applicant() { }
    public Applicant(Guid id, Guid tenantId, string applicationNumber, string firstName, string lastName, DateOnly dateOfBirth, string? email, string? phone, string? previousSchool, string? source, DateTimeOffset now)
    { if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Applicant identifiers are required."); Id = id; TenantId = tenantId; ApplicationNumber = Required(applicationNumber, 50).ToUpperInvariant(); FirstName = Required(firstName, 100); LastName = Required(lastName, 100); DateOfBirth = dateOfBirth; Email = Optional(email, 320); Phone = Optional(phone, 30); PreviousSchool = Optional(previousSchool, 200); Source = Optional(source, 100); Status = ApplicationStatus.Draft; CreatedAtUtc = now; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public string ApplicationNumber { get; private set; } = null!; public string FirstName { get; private set; } = null!; public string LastName { get; private set; } = null!; public DateOnly DateOfBirth { get; private set; } public string? Email { get; private set; } public string? Phone { get; private set; } public string? PreviousSchool { get; private set; } public string? Source { get; private set; } public ApplicationStatus Status { get; private set; } public Guid? StudentId { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; } public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public void Transition(ApplicationStatus next, DateTimeOffset now) { if (!Allowed(Status, next)) throw new InvalidOperationException($"Application cannot move from {Status} to {next}."); Status = next; UpdatedAtUtc = now; }
    public void MarkConverted(Guid studentId, DateTimeOffset now) { if (Status != ApplicationStatus.Accepted) throw new InvalidOperationException("Only accepted applicants can become students."); if (studentId == Guid.Empty) throw new ArgumentException("Student identifier is required."); StudentId = studentId; Status = ApplicationStatus.Converted; UpdatedAtUtc = now; }
    private static bool Allowed(ApplicationStatus current, ApplicationStatus next) => (current, next) switch { (ApplicationStatus.Draft, ApplicationStatus.Submitted) => true, (ApplicationStatus.Submitted, ApplicationStatus.UnderReview) => true, (ApplicationStatus.UnderReview, ApplicationStatus.Waitlisted or ApplicationStatus.Offered or ApplicationStatus.Rejected) => true, (ApplicationStatus.Waitlisted, ApplicationStatus.Offered or ApplicationStatus.Rejected) => true, (ApplicationStatus.Offered, ApplicationStatus.Accepted or ApplicationStatus.Rejected) => true, (_, ApplicationStatus.Withdrawn) when current != ApplicationStatus.Converted => true, _ => false };
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value is required and must not exceed {max} characters.") : value.Trim();
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public sealed class AdmissionOffer : ITenantOwned
{
    private AdmissionOffer() { }
    public AdmissionOffer(Guid id, Guid tenantId, Guid applicantId, string tokenHash, DateTimeOffset expiresAtUtc, DateTimeOffset createdAtUtc)
    { if (id == Guid.Empty || tenantId == Guid.Empty || applicantId == Guid.Empty || string.IsNullOrWhiteSpace(tokenHash)) throw new ArgumentException("Offer identifiers and token are required."); if (expiresAtUtc <= createdAtUtc) throw new ArgumentException("Offer expiry must be after creation."); Id = id; TenantId = tenantId; ApplicantId = applicantId; TokenHash = tokenHash; ExpiresAtUtc = expiresAtUtc; CreatedAtUtc = createdAtUtc; Response = AdmissionResponse.Pending; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid ApplicantId { get; private set; } public string TokenHash { get; private set; } = null!; public DateTimeOffset ExpiresAtUtc { get; private set; } public AdmissionResponse Response { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; } public DateTimeOffset? RespondedAtUtc { get; private set; }
    public void Respond(bool accepted, DateTimeOffset now) { if (Response != AdmissionResponse.Pending) throw new InvalidOperationException("This offer has already received a response."); if (now > ExpiresAtUtc) throw new InvalidOperationException("This offer has expired."); Response = accepted ? AdmissionResponse.Accepted : AdmissionResponse.Declined; RespondedAtUtc = now; }
    public void Supersede(DateTimeOffset now) { if (Response == AdmissionResponse.Pending) { Response = AdmissionResponse.Declined; RespondedAtUtc = now; } }
}

public sealed class Student : ITenantOwned
{
    private Student() { }
    public Student(Guid id, Guid tenantId, string admissionNumber, string firstName, string lastName, DateOnly dateOfBirth, Guid? sourceApplicantId, DateTimeOffset now, string? email = null)
    { if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Student identifiers are required."); Id = id; TenantId = tenantId; AdmissionNumber = Required(admissionNumber, 50).ToUpperInvariant(); FirstName = Required(firstName, 100); LastName = Required(lastName, 100); DateOfBirth = dateOfBirth; SourceApplicantId = sourceApplicantId; Email = Optional(email, 320); Status = StudentStatus.Active; CreatedAtUtc = now; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid? UserId { get; private set; } public string AdmissionNumber { get; private set; } = null!; public string FirstName { get; private set; } = null!; public string LastName { get; private set; } = null!; public DateOnly DateOfBirth { get; private set; } public string? Email { get; private set; } public Guid? SourceApplicantId { get; private set; } public StudentStatus Status { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    public void UpdatePersonalInformation(string firstName, string lastName, DateOnly dateOfBirth, string? email = null) { FirstName = Required(firstName, 100); LastName = Required(lastName, 100); DateOfBirth = dateOfBirth; Email = Optional(email, 320); }
    public void LinkUser(Guid userId) { if (userId == Guid.Empty) throw new ArgumentException("User identifier is required.", nameof(userId)); if (UserId.HasValue && UserId != userId) throw new InvalidOperationException("Student is already linked to another account."); UserId = userId; }
    public void Withdraw() { if (Status != StudentStatus.Active) throw new InvalidOperationException("Only active students can be withdrawn."); Status = StudentStatus.Withdrawn; }
    public void ReactivateForReturn() { if (Status == StudentStatus.Withdrawn) Status = StudentStatus.Active; else if (Status != StudentStatus.Active) throw new InvalidOperationException("Only active or withdrawn students can return for re-enrolment."); }
    public void Graduate() { if (Status != StudentStatus.Active) throw new InvalidOperationException("Only an active student can graduate."); Status = StudentStatus.Graduated; }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value is required and must not exceed {max} characters.") : value.Trim();
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public sealed class Guardian : ITenantOwned
{
    private Guardian() { }
    public Guardian(Guid id, Guid tenantId, string firstName, string lastName, string phone, string? email, DateTimeOffset now) { if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Guardian identifiers are required."); Id = id; TenantId = tenantId; FirstName = Required(firstName, 100); LastName = Required(lastName, 100); Phone = Required(phone, 30); Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(); CreatedAtUtc = now; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid? UserId { get; private set; } public string FirstName { get; private set; } = null!; public string LastName { get; private set; } = null!; public string Phone { get; private set; } = null!; public string? Email { get; private set; } public string? Address { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    public void Update(string firstName, string lastName, string phone, string? email, string? address = null) { FirstName = Required(firstName, 100); LastName = Required(lastName, 100); Phone = Required(phone, 30); Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().Length > 320 ? throw new ArgumentException("Email must not exceed 320 characters.", nameof(email)) : email.Trim(); Address = string.IsNullOrWhiteSpace(address) ? null : Required(address, 1000); }
    public void LinkUser(Guid userId) { if (userId == Guid.Empty) throw new ArgumentException("User identifier is required.", nameof(userId)); if (UserId.HasValue && UserId != userId) throw new InvalidOperationException("Guardian is already linked to another account."); UserId = userId; }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value is required and must not exceed {max} characters.") : value.Trim();
}

public sealed class StudentGuardian : ITenantOwned
{
    private StudentGuardian() { }
    public StudentGuardian(Guid tenantId, Guid studentId, Guid guardianId, GuardianRelationshipType relationship, bool isPrimary, bool isEmergencyContact, bool mayCollect) { TenantId = tenantId; StudentId = studentId; GuardianId = guardianId; Relationship = relationship; IsPrimary = isPrimary; IsEmergencyContact = isEmergencyContact; MayCollect = mayCollect; }
    public Guid TenantId { get; private set; } public Guid StudentId { get; private set; } public Guid GuardianId { get; private set; } public GuardianRelationshipType Relationship { get; private set; } public bool IsPrimary { get; private set; } public bool IsEmergencyContact { get; private set; } public bool MayCollect { get; private set; }
}

public sealed class Enrollment : ITenantOwned
{
    private Enrollment() { }
    public Enrollment(Guid id, Guid tenantId, Guid studentId, Guid academicYearId, Guid classSectionId, DateOnly enrolledOn, DateTimeOffset now) { if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Enrollment identifiers are required."); Id = id; TenantId = tenantId; StudentId = studentId; AcademicYearId = academicYearId; ClassSectionId = classSectionId; EnrolledOn = enrolledOn; Status = EnrollmentStatus.Active; CreatedAtUtc = now; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid StudentId { get; private set; } public Guid AcademicYearId { get; private set; } public Guid ClassSectionId { get; private set; } public DateOnly EnrolledOn { get; private set; } public EnrollmentStatus Status { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    public void Complete(EnrollmentStatus status) { if (Status != EnrollmentStatus.Active) throw new InvalidOperationException("Only an active enrolment can be completed."); if (status == EnrollmentStatus.Active) throw new ArgumentException("A terminal enrolment status is required.", nameof(status)); Status = status; }
}

public sealed class StudentProgression : ITenantOwned
{
    private StudentProgression() { }
    public StudentProgression(Guid id, Guid tenantId, Guid studentId, Guid fromEnrollmentId, Guid toEnrollmentId, StudentProgressionType type, string? reason, Guid processedByUserId, DateTimeOffset createdAtUtc)
    { if (id == Guid.Empty || tenantId == Guid.Empty || studentId == Guid.Empty || fromEnrollmentId == Guid.Empty || toEnrollmentId == Guid.Empty || processedByUserId == Guid.Empty) throw new ArgumentException("Progression identifiers are required."); Id = id; TenantId = tenantId; StudentId = studentId; FromEnrollmentId = fromEnrollmentId; ToEnrollmentId = toEnrollmentId; Type = type; Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim().Length > 1000 ? throw new ArgumentException("Progression reason must not exceed 1000 characters.") : reason.Trim(); ProcessedByUserId = processedByUserId; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid StudentId { get; private set; } public Guid FromEnrollmentId { get; private set; } public Guid ToEnrollmentId { get; private set; } public StudentProgressionType Type { get; private set; } public string? Reason { get; private set; } public Guid ProcessedByUserId { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class ApplicantSensitiveRecord : ITenantOwned
{
    private ApplicantSensitiveRecord() { }
    public ApplicantSensitiveRecord(Guid tenantId, Guid applicantId, string? address, string? medicalInformation, string? allergies, string? specialEducationalNeeds, DateTimeOffset now) { TenantId = tenantId; ApplicantId = applicantId; Update(address, medicalInformation, allergies, specialEducationalNeeds, now); }
    public Guid TenantId { get; private set; } public Guid ApplicantId { get; private set; } public string? Address { get; private set; } public string? MedicalInformation { get; private set; } public string? Allergies { get; private set; } public string? SpecialEducationalNeeds { get; private set; } public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void Update(string? address, string? medicalInformation, string? allergies, string? specialEducationalNeeds, DateTimeOffset now) { Address = Limit(address, 1000); MedicalInformation = Limit(medicalInformation, 2000); Allergies = Limit(allergies, 1000); SpecialEducationalNeeds = Limit(specialEducationalNeeds, 2000); UpdatedAtUtc = now; }
    private static string? Limit(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public sealed class StudentSensitiveRecord : ITenantOwned
{
    private StudentSensitiveRecord() { }
    public StudentSensitiveRecord(Guid tenantId, Guid studentId, string? address, string? medicalInformation, string? allergies, string? specialEducationalNeeds, string? privateNotes, DateTimeOffset now) { TenantId = tenantId; StudentId = studentId; Update(address, medicalInformation, allergies, specialEducationalNeeds, privateNotes, now); }
    public Guid TenantId { get; private set; } public Guid StudentId { get; private set; } public string? Address { get; private set; } public string? MedicalInformation { get; private set; } public string? Allergies { get; private set; } public string? SpecialEducationalNeeds { get; private set; } public string? PrivateNotes { get; private set; } public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void Update(string? address, string? medicalInformation, string? allergies, string? specialEducationalNeeds, string? privateNotes, DateTimeOffset now) { Address = Limit(address, 1000); MedicalInformation = Limit(medicalInformation, 2000); Allergies = Limit(allergies, 1000); SpecialEducationalNeeds = Limit(specialEducationalNeeds, 2000); PrivateNotes = Limit(privateNotes, 2000); UpdatedAtUtc = now; }
    private static string? Limit(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public sealed class AdmissionReview : ITenantOwned
{
    private AdmissionReview() { }
    public AdmissionReview(Guid tenantId, Guid applicantId, decimal score, string? screeningNotes, DateTimeOffset now) { TenantId = tenantId; ApplicantId = applicantId; Update(score, screeningNotes, now); }
    public Guid TenantId { get; private set; } public Guid ApplicantId { get; private set; } public decimal Score { get; private set; } public string? ScreeningNotes { get; private set; } public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void Update(decimal score, string? screeningNotes, DateTimeOffset now) { if (score is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(score), "Admission score must be between 0 and 100."); Score = decimal.Round(score, 2, MidpointRounding.AwayFromZero); ScreeningNotes = string.IsNullOrWhiteSpace(screeningNotes) ? null : screeningNotes.Trim().Length > 2000 ? throw new ArgumentException("Screening notes must not exceed 2000 characters.", nameof(screeningNotes)) : screeningNotes.Trim(); UpdatedAtUtc = now; }
}

public sealed class AdmissionInterview : ITenantOwned
{
    private AdmissionInterview() { }
    public AdmissionInterview(Guid id, Guid tenantId, Guid applicantId, DateTimeOffset scheduledAtUtc, string? location, DateTimeOffset now) { if (id == Guid.Empty || tenantId == Guid.Empty || applicantId == Guid.Empty) throw new ArgumentException("Interview identifiers are required."); Id = id; TenantId = tenantId; ApplicantId = applicantId; ScheduledAtUtc = scheduledAtUtc; Location = Limit(location, 300); Status = InterviewStatus.Scheduled; CreatedAtUtc = now; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid ApplicantId { get; private set; } public DateTimeOffset ScheduledAtUtc { get; private set; } public string? Location { get; private set; } public InterviewStatus Status { get; private set; } public string? OutcomeNotes { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; } public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public void Complete(InterviewStatus status, string? outcomeNotes, DateTimeOffset now) { if (Status != InterviewStatus.Scheduled) throw new InvalidOperationException("Only a scheduled interview can be completed or cancelled."); if (status == InterviewStatus.Scheduled) throw new ArgumentException("A terminal interview status is required.", nameof(status)); Status = status; OutcomeNotes = Limit(outcomeNotes, 2000); UpdatedAtUtc = now; }
    private static string? Limit(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}
