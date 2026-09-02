using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Hr.Domain;

public enum StaffCategory { Teaching, Administrative, NonTeaching }
public enum StaffStatus { Active, Suspended, Exited }
public enum TeachingAssignmentRole { SubjectTeacher, ClassTeacher, FormTeacher }

public sealed class Position : ITenantOwned
{
    private Position() { }
    public Position(Guid id, Guid tenantId, string name, string code, DateTimeOffset createdAtUtc)
    { ValidateIds(id, tenantId); Id = id; TenantId = tenantId; Name = Required(name, nameof(name), 150); Code = Required(code, nameof(code), 50).ToUpperInvariant(); IsActive = true; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public string Name { get; private set; } = null!; public string Code { get; private set; } = null!;
    public bool IsActive { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
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
        StaffNumber = Required(staffNumber, nameof(staffNumber), 50).ToUpperInvariant(); FirstName = Required(firstName, nameof(firstName), 100); LastName = Required(lastName, nameof(lastName), 100);
        Category = category; CampusId = campusId; DepartmentId = departmentId; PositionId = positionId; WorkEmail = Optional(workEmail, 320); Phone = Optional(phone, 30); HireDate = hireDate; UpdatedAtUtc = now;
    }
    public void LinkUser(Guid userId, DateTimeOffset now) { if (userId == Guid.Empty) throw new ArgumentException("User identifier is required.", nameof(userId)); UserId = userId; UpdatedAtUtc = now; }
    public void Suspend(DateTimeOffset now) { if (Status == StaffStatus.Exited) throw new InvalidOperationException("Exited staff cannot be suspended."); Status = StaffStatus.Suspended; UpdatedAtUtc = now; }
    public void Reactivate(DateTimeOffset now) { if (Status == StaffStatus.Exited) throw new InvalidOperationException("Exited staff cannot be reactivated."); Status = StaffStatus.Active; UpdatedAtUtc = now; }
    public void Exit(DateOnly exitDate, DateTimeOffset now) { if (exitDate < HireDate) throw new ArgumentException("Exit date cannot precede hire date.", nameof(exitDate)); Status = StaffStatus.Exited; ExitDate = exitDate; UpdatedAtUtc = now; }
    private static string Required(string value, string name, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"{name} is required and must not exceed {max} characters.", name) : value.Trim();
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public sealed class StaffSensitiveRecord : ITenantOwned
{
    private StaffSensitiveRecord() { }
    public StaffSensitiveRecord(Guid tenantId, Guid staffId, string? address, string? nextOfKinName, string? nextOfKinPhone, string? notes, DateTimeOffset updatedAtUtc)
    { TenantId = tenantId; StaffId = staffId; Update(address, nextOfKinName, nextOfKinPhone, notes, updatedAtUtc); }
    public Guid TenantId { get; private set; } public Guid StaffId { get; private set; } public string? Address { get; private set; } public string? NextOfKinName { get; private set; }
    public string? NextOfKinPhone { get; private set; } public string? Notes { get; private set; } public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void Update(string? address, string? nextOfKinName, string? nextOfKinPhone, string? notes, DateTimeOffset now)
    { Address = Limit(address, 1000); NextOfKinName = Limit(nextOfKinName, 200); NextOfKinPhone = Limit(nextOfKinPhone, 30); Notes = Limit(notes, 2000); UpdatedAtUtc = now; }
    private static string? Limit(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}

public sealed class TeachingAssignment : ITenantOwned
{
    private TeachingAssignment() { }
    public TeachingAssignment(Guid id, Guid tenantId, Guid staffId, Guid classSectionId, Guid? subjectId, TeachingAssignmentRole role, DateTimeOffset createdAtUtc)
    { if (id == Guid.Empty || tenantId == Guid.Empty || staffId == Guid.Empty || classSectionId == Guid.Empty) throw new ArgumentException("Teaching assignment identifiers are required."); if (role == TeachingAssignmentRole.SubjectTeacher && !subjectId.HasValue) throw new ArgumentException("A subject teacher assignment requires a subject.", nameof(subjectId)); if (role != TeachingAssignmentRole.SubjectTeacher && subjectId.HasValue) throw new ArgumentException("Class and form teacher assignments must not specify a subject.", nameof(subjectId)); Id = id; TenantId = tenantId; StaffId = staffId; ClassSectionId = classSectionId; SubjectId = subjectId; Role = role; CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid StaffId { get; private set; } public Guid ClassSectionId { get; private set; } public Guid? SubjectId { get; private set; } public TeachingAssignmentRole Role { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
}
