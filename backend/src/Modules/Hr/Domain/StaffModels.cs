using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Hr.Domain;

public enum StaffCategory { Teaching, Administrative, NonTeaching }
public enum StaffStatus { Active, Suspended, Exited, Away, NotCleared, Inactive, OnLeave, Retired, Resigned, Sacked, Left, Deceased }
public enum TeachingAssignmentRole { SubjectTeacher, ClassTeacher, FormTeacher }

public sealed class Position : ITenantOwned
{
    private Position() { }
    public Position(Guid id, Guid tenantId, string name, string code, StaffCategory category, bool isCustom, DateTimeOffset createdAtUtc)
    { ValidateIds(id, tenantId); Id = id; TenantId = tenantId; Name = Required(name, nameof(name), 150); Code = Required(code, nameof(code), 50).ToUpperInvariant(); Category = category; IsCustom = isCustom; IsActive = true; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public string Name { get; private set; } = null!; public string Code { get; private set; } = null!;
    public StaffCategory Category { get; private set; } public bool IsCustom { get; private set; }
    public bool IsActive { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    public void Update(string name, StaffCategory category) { Name = Required(name, nameof(name), 150); Category = category; }
    private static void ValidateIds(Guid id, Guid tenantId) { if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Position identifiers are required."); }
    private static string Required(string value, string name, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"{name} is required and must not exceed {max} characters.", name) : value.Trim();
}

public sealed class StaffProfile : ITenantOwned
{
    private StaffProfile() { }
    public StaffProfile(Guid id, Guid tenantId, string staffNumber, string firstName, string lastName, StaffCategory category, Guid campusId,
        Guid? departmentId, Guid? positionId, string? workEmail, string? phone, DateOnly hireDate, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || campusId == Guid.Empty) throw new ArgumentException("Staff, tenant, and campus identifiers are required.");
        Id = id; TenantId = tenantId; CreatedAtUtc = createdAtUtc; Status = StaffStatus.Active;
        Update(staffNumber, firstName, lastName, category, campusId, departmentId, positionId, workEmail, phone, hireDate, createdAtUtc);
        UpdatedAtUtc = null;
    }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid? UserId { get; private set; }
    public string StaffNumber { get; private set; } = null!; public string FirstName { get; private set; } = null!; public string LastName { get; private set; } = null!;
    public StaffCategory Category { get; private set; } public StaffStatus Status { get; private set; } public Guid CampusId { get; private set; }
    public Guid? DepartmentId { get; private set; } public Guid? PositionId { get; private set; } public string? WorkEmail { get; private set; } public string? Phone { get; private set; }
    public DateOnly HireDate { get; private set; } public DateOnly? ExitDate { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; } public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public void Update(string staffNumber, string firstName, string lastName, StaffCategory category, Guid campusId, Guid? departmentId, Guid? positionId,
        string? workEmail, string? phone, DateOnly hireDate, DateTimeOffset now)
    {
        StaffNumber = Required(staffNumber, nameof(staffNumber), 50).ToUpperInvariant(); FirstName = StaffFieldNormalization.Name(firstName, nameof(firstName), 100); LastName = StaffFieldNormalization.Name(lastName, nameof(lastName), 100);
        Category = category; CampusId = campusId; DepartmentId = departmentId; PositionId = positionId; WorkEmail = StaffFieldNormalization.Email(workEmail); Phone = StaffFieldNormalization.Phone(phone); HireDate = hireDate; UpdatedAtUtc = now;
    }
    public void LinkUser(Guid userId, DateTimeOffset now) { if (userId == Guid.Empty) throw new ArgumentException("User identifier is required.", nameof(userId)); UserId = userId; UpdatedAtUtc = now; }
    public void Suspend(DateTimeOffset now) { if (Status == StaffStatus.Exited) throw new InvalidOperationException("Exited staff cannot be suspended."); Status = StaffStatus.Suspended; UpdatedAtUtc = now; }
    public void Reactivate(DateTimeOffset now) { if (Status == StaffStatus.Exited) throw new InvalidOperationException("Exited staff cannot be reactivated."); Status = StaffStatus.Active; UpdatedAtUtc = now; }
    public void SetInitialStatus(StaffStatus status, DateTimeOffset now) { if (status == StaffStatus.Exited) throw new ArgumentException("Exited is not a valid initial staff status.", nameof(status)); Status = status; UpdatedAtUtc = now; }
    public void SetOperationalStatus(StaffStatus status, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status) || status == StaffStatus.Exited) throw new ArgumentException("Choose a valid non-exited staff status.", nameof(status));
        if (Status == StaffStatus.Exited) throw new InvalidOperationException("Exited staff cannot be reactivated through a status edit.");
        Status = status;
        UpdatedAtUtc = now;
    }
    public void Exit(DateOnly exitDate, DateTimeOffset now) { if (exitDate < HireDate) throw new ArgumentException("Exit date cannot precede hire date.", nameof(exitDate)); Status = StaffStatus.Exited; ExitDate = exitDate; UpdatedAtUtc = now; }
    private static string Required(string value, string name, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"{name} is required and must not exceed {max} characters.", name) : value.Trim();
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public sealed class StaffSensitiveRecord : ITenantOwned
{
    private StaffSensitiveRecord() { }
    public StaffSensitiveRecord(Guid tenantId, Guid staffId, string? address, string? nextOfKinName, string? nextOfKinPhone, string? notes, string? title, string? middleName,
        string? gender, DateOnly? dateOfBirth, string? maritalStatus, string? religion, string? country, string? state, string? localGovernment, string? city,
        string? genotype, string? bloodGroup, decimal? weightKg, decimal? heightCm, string? disability, string? skills, string? achievements,
        string? website, string? officeAddress, string? socialProfilesJson, DateTimeOffset updatedAtUtc)
    { TenantId = tenantId; StaffId = staffId; Update(address, nextOfKinName, nextOfKinPhone, notes, title, middleName, gender, dateOfBirth, maritalStatus, religion, country, state, localGovernment, city, genotype, bloodGroup, weightKg, heightCm, disability, skills, achievements, website, officeAddress, socialProfilesJson, updatedAtUtc); }
    public Guid TenantId { get; private set; } public Guid StaffId { get; private set; } public string? Address { get; private set; } public string? NextOfKinName { get; private set; }
    public string? NextOfKinPhone { get; private set; } public string? Notes { get; private set; } public DateTimeOffset UpdatedAtUtc { get; private set; }
    public string? Title { get; private set; } public string? MiddleName { get; private set; } public string? Gender { get; private set; } public DateOnly? DateOfBirth { get; private set; }
    public string? MaritalStatus { get; private set; } public string? Religion { get; private set; } public string? Country { get; private set; } public string? State { get; private set; }
    public string? LocalGovernment { get; private set; } public string? City { get; private set; } public string? Genotype { get; private set; } public string? BloodGroup { get; private set; }
    public decimal? WeightKg { get; private set; } public decimal? HeightCm { get; private set; } public string? Disability { get; private set; } public string? Skills { get; private set; }
    public string? Achievements { get; private set; } public string? Website { get; private set; } public string? OfficeAddress { get; private set; } public string? SocialProfilesJson { get; private set; }
    public void Update(string? address, string? nextOfKinName, string? nextOfKinPhone, string? notes, string? title, string? middleName, string? gender, DateOnly? dateOfBirth,
        string? maritalStatus, string? religion, string? country, string? state, string? localGovernment, string? city, string? genotype, string? bloodGroup,
        decimal? weightKg, decimal? heightCm, string? disability, string? skills, string? achievements, string? website, string? officeAddress, string? socialProfilesJson, DateTimeOffset now)
    {
        Address = Limit(address, 1000); NextOfKinName = Limit(nextOfKinName, 200); NextOfKinPhone = Limit(nextOfKinPhone, 30); Notes = Limit(notes, 2000);
        Title = Limit(title, 30); MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : StaffFieldNormalization.Name(middleName, nameof(middleName), 100); Gender = Limit(gender, 30); DateOfBirth = dateOfBirth; MaritalStatus = Limit(maritalStatus, 40);
        Religion = Limit(religion, 80); Country = Limit(country, 100); State = Limit(state, 100); LocalGovernment = Limit(localGovernment, 150); City = Limit(city, 150);
        Genotype = Limit(genotype, 10)?.ToUpperInvariant(); BloodGroup = Limit(bloodGroup, 10)?.ToUpperInvariant(); WeightKg = Positive(weightKg, 500, nameof(weightKg)); HeightCm = Positive(heightCm, 300, nameof(heightCm));
        Disability = Limit(disability, 1000); Skills = Limit(skills, 2000); Achievements = Limit(achievements, 3000); Website = Limit(website, 500);
        OfficeAddress = Limit(officeAddress, 1000); SocialProfilesJson = Limit(socialProfilesJson, 4000); UpdatedAtUtc = now;
    }
    private static decimal? Positive(decimal? value, decimal max, string name) => value is null ? null : value <= 0 || value > max ? throw new ArgumentException($"{name} is outside the permitted range.", name) : value;
    private static string? Limit(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public sealed class StaffEmploymentRecord : ITenantOwned
{
    private StaffEmploymentRecord() { }
    public StaffEmploymentRecord(Guid id, Guid tenantId, Guid staffId, string employerName, string jobTitle, DateOnly startedOn, DateOnly? endedOn, string? reasonForLeaving, DateTimeOffset createdAtUtc)
    {
        ValidateIds(id, tenantId, staffId); if (endedOn.HasValue && endedOn < startedOn) throw new ArgumentException("Employment end date cannot precede its start date.", nameof(endedOn));
        Id = id; TenantId = tenantId; StaffId = staffId; EmployerName = Required(employerName, nameof(employerName), 200); JobTitle = Required(jobTitle, nameof(jobTitle), 150);
        StartedOn = startedOn; EndedOn = endedOn; ReasonForLeaving = Optional(reasonForLeaving, 500); CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid StaffId { get; private set; }
    public string EmployerName { get; private set; } = null!; public string JobTitle { get; private set; } = null!; public DateOnly StartedOn { get; private set; }
    public DateOnly? EndedOn { get; private set; } public string? ReasonForLeaving { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    private static void ValidateIds(params Guid[] ids) { if (ids.Any(x => x == Guid.Empty)) throw new ArgumentException("Employment record identifiers are required."); }
    private static string Required(string value, string name, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"{name} is required and must not exceed {max} characters.", name) : value.Trim();
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public sealed class StaffQualification : ITenantOwned
{
    private StaffQualification() { }
    public StaffQualification(Guid id, Guid tenantId, Guid staffId, string institution, string name, string? fieldOfStudy, DateOnly awardedOn, string? grade, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || staffId == Guid.Empty) throw new ArgumentException("Qualification identifiers are required.");
        Id = id; TenantId = tenantId; StaffId = staffId; Institution = Required(institution, nameof(institution), 200); Name = Required(name, nameof(name), 200);
        FieldOfStudy = Optional(fieldOfStudy, 150); AwardedOn = awardedOn; Grade = Optional(grade, 100); CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid StaffId { get; private set; } public string Institution { get; private set; } = null!;
    public string Name { get; private set; } = null!; public string? FieldOfStudy { get; private set; } public DateOnly AwardedOn { get; private set; } public string? Grade { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    private static string Required(string value, string name, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"{name} is required and must not exceed {max} characters.", name) : value.Trim();
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public sealed class StaffNextOfKin : ITenantOwned
{
    private StaffNextOfKin() { }
    public StaffNextOfKin(Guid id, Guid tenantId, Guid staffId, string fullName, string relationship, string phone, string? email, string? address, bool isPrimary, DateTimeOffset createdAtUtc)
    { if (id == Guid.Empty || tenantId == Guid.Empty || staffId == Guid.Empty) throw new ArgumentException("Next-of-kin identifiers are required."); Id = id; TenantId = tenantId; StaffId = staffId; CreatedAtUtc = createdAtUtc; Update(fullName, relationship, phone, email, address, isPrimary, createdAtUtc); }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid StaffId { get; private set; } public string FullName { get; private set; } = null!;
    public string Relationship { get; private set; } = null!; public string Phone { get; private set; } = null!; public string? Email { get; private set; } public string? Address { get; private set; }
    public bool IsPrimary { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; } public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void Update(string fullName, string relationship, string phone, string? email, string? address, bool isPrimary, DateTimeOffset now)
    { FullName = StaffFieldNormalization.Name(fullName, nameof(fullName), 200); Relationship = Required(relationship, nameof(relationship), 100); Phone = StaffFieldNormalization.Phone(phone) ?? throw new ArgumentException("Phone number is required.", nameof(phone)); Email = StaffFieldNormalization.Email(email); Address = Optional(address, 1000); IsPrimary = isPrimary; UpdatedAtUtc = now; }
    public void RemovePrimary(DateTimeOffset now) { IsPrimary = false; UpdatedAtUtc = now; }
    private static string Required(string value, string name, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"{name} is required and must not exceed {max} characters.", name) : value.Trim();
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public static class StaffFieldNormalization
{
    public static string Name(string value, string field, int maxLength)
    {
        var normalized = string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length is 0 || normalized.Length > maxLength)
            throw new ArgumentException($"{field} is required and must not exceed {maxLength} characters.", field);
        return normalized;
    }

    public static string? Email(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 320 || !System.Net.Mail.MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)
            throw new ArgumentException("Enter a valid email address.", nameof(value));
        return normalized;
    }

    public static string? Phone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length != 11 || normalized.Any(character => !char.IsAsciiDigit(character)))
            throw new ArgumentException("Phone number must contain exactly 11 digits.", nameof(value));
        return normalized;
    }
}

public sealed class TeachingAssignment : ITenantOwned
{
    private TeachingAssignment() { }
    public TeachingAssignment(Guid id, Guid tenantId, Guid staffId, Guid classSectionId, Guid? subjectId, TeachingAssignmentRole role, DateTimeOffset createdAtUtc)
    { if (id == Guid.Empty || tenantId == Guid.Empty || staffId == Guid.Empty || classSectionId == Guid.Empty) throw new ArgumentException("Teaching assignment identifiers are required."); if (role == TeachingAssignmentRole.SubjectTeacher && !subjectId.HasValue) throw new ArgumentException("A subject teacher assignment requires a subject.", nameof(subjectId)); if (role != TeachingAssignmentRole.SubjectTeacher && subjectId.HasValue) throw new ArgumentException("Class and form teacher assignments must not specify a subject.", nameof(subjectId)); Id = id; TenantId = tenantId; StaffId = staffId; ClassSectionId = classSectionId; SubjectId = subjectId; Role = role; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid StaffId { get; private set; } public Guid ClassSectionId { get; private set; } public Guid? SubjectId { get; private set; } public TeachingAssignmentRole Role { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
}
