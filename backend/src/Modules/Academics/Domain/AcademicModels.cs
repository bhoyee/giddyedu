using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Academics.Domain;

public enum AcademicPeriodStatus { Planned, Active, Closed }

public sealed class AcademicYear : ITenantOwned
{
    private AcademicYear() { }
    public AcademicYear(Guid id, Guid tenantId, string name, DateOnly startsOn, DateOnly endsOn, DateTimeOffset createdAtUtc)
    {
        ValidateIds(id, tenantId); if (endsOn <= startsOn) throw new ArgumentException("Academic year end date must be after its start date.");
        Id = id; TenantId = tenantId; Name = Required(name, nameof(name), 100); StartsOn = startsOn; EndsOn = endsOn; Status = AcademicPeriodStatus.Planned; CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public string Name { get; private set; } = null!;
    public DateOnly StartsOn { get; private set; } public DateOnly EndsOn { get; private set; } public AcademicPeriodStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public void Activate(DateTimeOffset now) { if (Status == AcademicPeriodStatus.Closed) throw new InvalidOperationException("A closed academic year cannot be reactivated."); Status = AcademicPeriodStatus.Active; UpdatedAtUtc = now; }
    public void Update(string name, DateOnly startsOn, DateOnly endsOn, DateTimeOffset now) { if (endsOn <= startsOn) throw new ArgumentException("Academic year end date must be after its start date."); Name = Required(name, nameof(name), 100); StartsOn = startsOn; EndsOn = endsOn; UpdatedAtUtc = now; }
    public void Close(DateTimeOffset now) { Status = AcademicPeriodStatus.Closed; UpdatedAtUtc = now; }
    private static void ValidateIds(Guid id, Guid tenantId) { if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Academic year identifiers are required."); }
    private static string Required(string value, string name, int max) => DomainValidation.Required(value, name, max);
}

public sealed class AcademicTerm : ITenantOwned
{
    private AcademicTerm() { }
    public AcademicTerm(Guid id, Guid tenantId, Guid academicYearId, string name, string code, int sequence, DateOnly startsOn, DateOnly endsOn, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || academicYearId == Guid.Empty) throw new ArgumentException("Term identifiers are required.");
        if (sequence < 1 || endsOn <= startsOn) throw new ArgumentException("Term sequence and date range are invalid.");
        Id = id; TenantId = tenantId; AcademicYearId = academicYearId; Name = DomainValidation.Required(name, nameof(name), 100); Code = DomainValidation.Code(code);
        Sequence = sequence; StartsOn = startsOn; EndsOn = endsOn; Status = AcademicPeriodStatus.Planned; CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid AcademicYearId { get; private set; }
    public string Name { get; private set; } = null!; public string Code { get; private set; } = null!; public int Sequence { get; private set; }
    public DateOnly StartsOn { get; private set; } public DateOnly EndsOn { get; private set; } public AcademicPeriodStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public void Update(Guid academicYearId, string name, string code, int sequence, DateOnly startsOn, DateOnly endsOn) { if (academicYearId == Guid.Empty || sequence < 1 || endsOn <= startsOn) throw new ArgumentException("Term sequence and date range are invalid."); AcademicYearId = academicYearId; Name = DomainValidation.Required(name, nameof(name), 100); Code = DomainValidation.Code(code); Sequence = sequence; StartsOn = startsOn; EndsOn = endsOn; }
    public void Activate() { if (Status == AcademicPeriodStatus.Closed) throw new InvalidOperationException("A closed term cannot be reactivated."); Status = AcademicPeriodStatus.Active; }
    public void Close() => Status = AcademicPeriodStatus.Closed;
}

public sealed class EducationStage : ITenantOwned
{
    private EducationStage() { }
    public EducationStage(Guid id, Guid tenantId, string name, string code, int displayOrder, DateTimeOffset createdAtUtc)
    { DomainValidation.Ids(id, tenantId); Id = id; TenantId = tenantId; Name = DomainValidation.Required(name, nameof(name), 100); Code = DomainValidation.Code(code); DisplayOrder = displayOrder; IsActive = true; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public string Name { get; private set; } = null!; public string Code { get; private set; } = null!;
    public int DisplayOrder { get; private set; } public bool IsActive { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    public void Update(string name, string code, int displayOrder) { Name = DomainValidation.Required(name, nameof(name), 100); Code = DomainValidation.Code(code); DisplayOrder = displayOrder; }
}

public sealed class ClassLevel : ITenantOwned
{
    private ClassLevel() { }
    public ClassLevel(Guid id, Guid tenantId, Guid educationStageId, string name, string code, int displayOrder, DateTimeOffset createdAtUtc)
    { DomainValidation.Ids(id, tenantId); if (educationStageId == Guid.Empty) throw new ArgumentException("Education stage is required."); Id = id; TenantId = tenantId; EducationStageId = educationStageId; Name = DomainValidation.Required(name, nameof(name), 100); Code = DomainValidation.Code(code); DisplayOrder = displayOrder; IsActive = true; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid EducationStageId { get; private set; }
    public string Name { get; private set; } = null!; public string Code { get; private set; } = null!; public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    public void Update(Guid stageId, string name, string code, int displayOrder) { if (stageId == Guid.Empty) throw new ArgumentException("Education stage is required."); EducationStageId = stageId; Name = DomainValidation.Required(name, nameof(name), 100); Code = DomainValidation.Code(code); DisplayOrder = displayOrder; }
}

public sealed class ClassSection : ITenantOwned
{
    private ClassSection() { }
    public ClassSection(Guid id, Guid tenantId, Guid campusId, Guid academicYearId, Guid classLevelId, string name, string code, int? capacity, DateTimeOffset createdAtUtc)
    {
        DomainValidation.Ids(id, tenantId); if (campusId == Guid.Empty || academicYearId == Guid.Empty || classLevelId == Guid.Empty) throw new ArgumentException("Campus, academic year, and class level are required.");
        if (capacity is <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Id = id; TenantId = tenantId; CampusId = campusId; AcademicYearId = academicYearId; ClassLevelId = classLevelId;
        Name = DomainValidation.Required(name, nameof(name), 100); Code = DomainValidation.Code(code); Capacity = capacity; IsActive = true; CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid CampusId { get; private set; } public Guid AcademicYearId { get; private set; } public Guid ClassLevelId { get; private set; }
    public string Name { get; private set; } = null!; public string Code { get; private set; } = null!; public int? Capacity { get; private set; } public bool IsActive { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    public void Update(Guid campusId, Guid yearId, Guid levelId, string name, string code, int? capacity) { if (campusId == Guid.Empty || yearId == Guid.Empty || levelId == Guid.Empty || capacity is <= 0) throw new ArgumentException("Class section details are invalid."); CampusId = campusId; AcademicYearId = yearId; ClassLevelId = levelId; Name = DomainValidation.Required(name, nameof(name), 100); Code = DomainValidation.Code(code); Capacity = capacity; }
}

public sealed class Department : ITenantOwned
{
    private Department() { }
    public Department(Guid id, Guid tenantId, string name, string code, DateTimeOffset createdAtUtc) { DomainValidation.Ids(id, tenantId); Id = id; TenantId = tenantId; Name = DomainValidation.Required(name, nameof(name), 150); Code = DomainValidation.Code(code); IsActive = true; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public string Name { get; private set; } = null!; public string Code { get; private set; } = null!; public bool IsActive { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    public void Update(string name, string code) { Name = DomainValidation.Required(name, nameof(name), 150); Code = DomainValidation.Code(code); }
}

public sealed class Subject : ITenantOwned
{
    private Subject() { }
    public Subject(Guid id, Guid tenantId, Guid? departmentId, string name, string code, bool isCore, DateTimeOffset createdAtUtc) { DomainValidation.Ids(id, tenantId); Id = id; TenantId = tenantId; DepartmentId = departmentId; Name = DomainValidation.Required(name, nameof(name), 150); Code = DomainValidation.Code(code); IsCore = isCore; IsActive = true; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid? DepartmentId { get; private set; } public string Name { get; private set; } = null!; public string Code { get; private set; } = null!; public bool IsCore { get; private set; } public bool IsActive { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
    public void Update(Guid? departmentId, string name, string code, bool isCore) { DepartmentId = departmentId; Name = DomainValidation.Required(name, nameof(name), 150); Code = DomainValidation.Code(code); IsCore = isCore; }
}

public sealed class ClassSubject : ITenantOwned
{
    private ClassSubject() { }
    public ClassSubject(Guid tenantId, Guid classSectionId, Guid subjectId, bool isCompulsory) { if (tenantId == Guid.Empty || classSectionId == Guid.Empty || subjectId == Guid.Empty) throw new ArgumentException("Class-subject identifiers are required."); TenantId = tenantId; ClassSectionId = classSectionId; SubjectId = subjectId; IsCompulsory = isCompulsory; }
    public Guid TenantId { get; private set; } public Guid ClassSectionId { get; private set; } public Guid SubjectId { get; private set; } public bool IsCompulsory { get; private set; }
    public void Update(bool isCompulsory) => IsCompulsory = isCompulsory;
}

internal static class DomainValidation
{
    public static void Ids(Guid id, Guid tenantId) { if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Entity and tenant identifiers are required."); }
    public static string Required(string value, string name, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"{name} is required and must not exceed {max} characters.", name) : value.Trim();
    public static string Code(string value) => Required(value, nameof(value), 50).ToUpperInvariant();
}
