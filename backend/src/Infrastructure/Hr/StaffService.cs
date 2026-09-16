using GiddyEdu.BuildingBlocks.Api;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Hr.Domain;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Hr;

public sealed record PositionInput(string Name, StaffCategory Category = StaffCategory.Administrative);
public sealed record StaffInput(string? StaffNumber, string FirstName, string LastName, StaffCategory Category, Guid CampusId,
    Guid? DepartmentId, Guid? PositionId, string? Email, string? Phone, DateOnly HireDate, StaffStatus Status = StaffStatus.Active);
public sealed record StaffStatusInput(StaffStatus Status, DateOnly? ExitDate);
public sealed record StaffUserLinkInput(Guid UserId);
public sealed record StaffSensitiveInput(string? Address, string? NextOfKinName, string? NextOfKinPhone, string? Notes, string? Title = null, string? MiddleName = null,
    string? Gender = null, DateOnly? DateOfBirth = null, string? MaritalStatus = null, string? Religion = null, string? Country = null, string? State = null,
    string? LocalGovernment = null, string? City = null, string? Genotype = null, string? BloodGroup = null, decimal? WeightKg = null, decimal? HeightCm = null,
    string? Disability = null, string? Skills = null, string? Achievements = null, string? Website = null, string? OfficeAddress = null, string? SocialProfilesJson = null);
public sealed record PositionInfo(Guid Id, string Name, string Code, StaffCategory Category, string CategoryName, bool IsCustom, bool IsActive);
public sealed record StaffInfo(Guid Id, Guid? UserId, string StaffNumber, string FirstName, string LastName, StaffCategory Category,
    StaffStatus Status, Guid CampusId, Guid? DepartmentId, Guid? PositionId, string? WorkEmail, string? Phone, DateOnly HireDate, DateOnly? ExitDate);
public sealed record StaffSensitiveInfo(string? Address, string? NextOfKinName, string? NextOfKinPhone, string? Notes, string? Title, string? MiddleName, string? Gender,
    DateOnly? DateOfBirth, string? MaritalStatus, string? Religion, string? Country, string? State, string? LocalGovernment, string? City, string? Genotype,
    string? BloodGroup, decimal? WeightKg, decimal? HeightCm, string? Disability, string? Skills, string? Achievements, string? Website, string? OfficeAddress,
    string? SocialProfilesJson, DateTimeOffset UpdatedAtUtc);
public sealed record StaffEmploymentInput(string EmployerName, string JobTitle, DateOnly StartedOn, DateOnly? EndedOn, string? ReasonForLeaving);
public sealed record StaffEmploymentInfo(Guid Id, string EmployerName, string JobTitle, DateOnly StartedOn, DateOnly? EndedOn, string? ReasonForLeaving);
public sealed record StaffQualificationInput(string Institution, string Name, string? FieldOfStudy, DateOnly AwardedOn, string? Grade);
public sealed record StaffQualificationInfo(Guid Id, string Institution, string Name, string? FieldOfStudy, DateOnly AwardedOn, string? Grade);
public sealed record StaffNextOfKinInput(string FullName, string Relationship, string Phone, string? Email, string? Address, bool IsPrimary);
public sealed record StaffNextOfKinInfo(Guid Id, string FullName, string Relationship, string Phone, string? Email, string? Address, bool IsPrimary);
public sealed record TeachingAssignmentInput(Guid StaffId, Guid ClassSectionId, Guid? SubjectId, TeachingAssignmentRole Role);
public sealed record TeachingAssignmentInfo(Guid Id, Guid StaffId, Guid ClassSectionId, Guid? SubjectId, TeachingAssignmentRole Role);

public interface IStaffService
{
    Task<IReadOnlyList<PositionInfo>> ListPositionsAsync(Guid actor, CancellationToken ct = default);
    Task<Guid> CreatePositionAsync(Guid actor, PositionInput input, CancellationToken ct = default);
    Task UpdatePositionAsync(Guid actor, Guid positionId, PositionInput input, CancellationToken ct = default);
    Task DeletePositionAsync(Guid actor, Guid positionId, CancellationToken ct = default);
    Task<PageResult<StaffInfo>> ListAsync(Guid actor, int page, int pageSize, string? search, StaffStatus? status, CancellationToken ct = default);
    Task<StaffInfo> GetAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid actor, StaffInput input, CancellationToken ct = default);
    Task UpdateAsync(Guid actor, Guid id, StaffInput input, CancellationToken ct = default);
    Task SetStatusAsync(Guid actor, Guid id, StaffStatusInput input, CancellationToken ct = default);
    Task LinkUserAsync(Guid actor, Guid id, StaffUserLinkInput input, CancellationToken ct = default);
    Task<StaffSensitiveInfo?> GetSensitiveAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task UpsertSensitiveAsync(Guid actor, Guid id, StaffSensitiveInput input, CancellationToken ct = default);
    Task<IReadOnlyList<StaffEmploymentInfo>> ListEmploymentAsync(Guid actor, Guid staffId, CancellationToken ct = default);
    Task<Guid> AddEmploymentAsync(Guid actor, Guid staffId, StaffEmploymentInput input, CancellationToken ct = default);
    Task DeleteEmploymentAsync(Guid actor, Guid staffId, Guid recordId, CancellationToken ct = default);
    Task<IReadOnlyList<StaffQualificationInfo>> ListQualificationsAsync(Guid actor, Guid staffId, CancellationToken ct = default);
    Task<Guid> AddQualificationAsync(Guid actor, Guid staffId, StaffQualificationInput input, CancellationToken ct = default);
    Task DeleteQualificationAsync(Guid actor, Guid staffId, Guid qualificationId, CancellationToken ct = default);
    Task<IReadOnlyList<StaffNextOfKinInfo>> ListNextOfKinAsync(Guid actor, Guid staffId, CancellationToken ct = default);
    Task<Guid> AddNextOfKinAsync(Guid actor, Guid staffId, StaffNextOfKinInput input, CancellationToken ct = default);
    Task UpdateNextOfKinAsync(Guid actor, Guid staffId, Guid contactId, StaffNextOfKinInput input, CancellationToken ct = default);
    Task DeleteNextOfKinAsync(Guid actor, Guid staffId, Guid contactId, CancellationToken ct = default);
    Task<IReadOnlyList<TeachingAssignmentInfo>> ListTeachingAssignmentsAsync(Guid actor, Guid? classSectionId, CancellationToken ct = default);
    Task<Guid> CreateTeachingAssignmentAsync(Guid actor, TeachingAssignmentInput input, CancellationToken ct = default);
    Task DeleteTeachingAssignmentAsync(Guid actor, Guid assignmentId, CancellationToken ct = default);
}

public sealed class StaffService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IPermissionService permissions, IClock clock) : IStaffService
{
    public async Task<IReadOnlyList<PositionInfo>> ListPositionsAsync(Guid actor, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); await EnsureDefaultPositionsAsync(ct); return await db.Positions.AsNoTracking().OrderBy(x => x.Category).ThenBy(x => x.Name).Select(x => new PositionInfo(x.Id, x.Name, x.Code, x.Category, x.Category == StaffCategory.Teaching ? "Teaching" : x.Category == StaffCategory.Administrative ? "Administrative" : "Non-teaching", x.IsCustom, x.IsActive)).ToListAsync(ct); }

    public async Task<Guid> CreatePositionAsync(Guid actor, PositionInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var name = ValidatePositionName(input);
        if (await db.Positions.AnyAsync(x => x.Category == input.Category && x.Name.ToLower() == name.ToLower(), ct)) throw new InvalidOperationException("A position with this name already exists in the selected category.");
        var code = await GeneratePositionCodeAsync(name, ct);
        var id = Guid.NewGuid();
        db.Positions.Add(new Position(id, RequireTenant(), name, code, input.Category, true, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task UpdatePositionAsync(Guid actor, Guid positionId, PositionInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var name = ValidatePositionName(input);
        if (await db.Positions.AnyAsync(x => x.Id != positionId && x.Category == input.Category && x.Name.ToLower() == name.ToLower(), ct))
            throw new InvalidOperationException("A position with this name already exists.");
        var position = await db.Positions.SingleOrDefaultAsync(x => x.Id == positionId, ct)
            ?? throw new KeyNotFoundException("Position was not found.");
        if (!position.IsCustom) throw new InvalidOperationException("Default positions cannot be edited.");
        position.Update(name, input.Category);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeletePositionAsync(Guid actor, Guid positionId, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var position = await db.Positions.SingleOrDefaultAsync(x => x.Id == positionId, ct)
            ?? throw new KeyNotFoundException("Position was not found.");
        if (!position.IsCustom) throw new InvalidOperationException("Default positions cannot be deleted.");
        if (await db.StaffProfiles.AnyAsync(x => x.PositionId == positionId, ct))
            throw new InvalidOperationException("This position is assigned to staff and cannot be deleted. Reassign those staff members first.");
        db.Positions.Remove(position);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PageResult<StaffInfo>> ListAsync(Guid actor, int page, int pageSize, string? search, StaffStatus? status, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.StaffView, ct); RequireTenant(); page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.StaffProfiles.AsNoTracking().Where(x => !status.HasValue || x.Status == status);
        if (!await permissions.HasPermissionAsync(actor, Permissions.StaffManage, ct)) query = query.Where(x => x.UserId == actor);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => x.StaffNumber.Contains(term) || x.FirstName.Contains(term) || x.LastName.Contains(term)); }
        var total = await query.LongCountAsync(ct); var items = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Skip((page - 1) * pageSize).Take(pageSize).Select(Project()).ToListAsync(ct);
        return new(items, page, pageSize, total);
    }

    public async Task<StaffInfo> GetAsync(Guid actor, Guid id, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); RequireTenant(); var query = db.StaffProfiles.AsNoTracking().Where(x => x.Id == id); if (!await permissions.HasPermissionAsync(actor, Permissions.StaffManage, ct)) query = query.Where(x => x.UserId == actor); return await query.Select(Project()).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Staff member was not found."); }

    public async Task<Guid> CreateAsync(Guid actor, StaffInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var campusId = await ResolveCampusAsync(input.CampusId, null, ct); await ValidateReferencesAsync(input, campusId, ct); ValidateContact(input); await EnsureUniqueContactAsync(input.Email!, input.Phone!, null, ct); var id = Guid.NewGuid(); var staffNumber = await GenerateStaffNumberAsync(ct); var staff = new StaffProfile(id, RequireTenant(), staffNumber, input.FirstName, input.LastName, input.Category, campusId, input.DepartmentId, input.PositionId, input.Email, input.Phone, input.HireDate, clock.UtcNow); staff.SetInitialStatus(input.Status, clock.UtcNow); db.StaffProfiles.Add(staff); await db.SaveChangesAsync(ct); return id; }

    public async Task UpdateAsync(Guid actor, Guid id, StaffInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var staff = await FindAsync(id, ct); var campusId = await ResolveCampusAsync(input.CampusId, staff.CampusId, ct); await ValidateReferencesAsync(input, campusId, ct); ValidateContact(input); await EnsureUniqueContactAsync(input.Email!, input.Phone!, id, ct); staff.Update(staff.StaffNumber, input.FirstName, input.LastName, input.Category, campusId, input.DepartmentId, input.PositionId, input.Email, input.Phone, input.HireDate, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task SetStatusAsync(Guid actor, Guid id, StaffStatusInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct); var staff = await FindAsync(id, ct);
        switch (input.Status) { case StaffStatus.Active: staff.Reactivate(clock.UtcNow); break; case StaffStatus.Suspended: staff.Suspend(clock.UtcNow); break; case StaffStatus.Exited when input.ExitDate.HasValue: staff.Exit(input.ExitDate.Value, clock.UtcNow); break; case StaffStatus.Exited: throw new ArgumentException("Exit date is required.", nameof(input)); }
        await db.SaveChangesAsync(ct);
    }

    public async Task LinkUserAsync(Guid actor, Guid id, StaffUserLinkInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); if (!await db.TenantMemberships.AnyAsync(x => x.UserId == input.UserId && x.IsActive, ct)) throw new InvalidOperationException("User must have an active membership in the current tenant."); var staff = await FindAsync(id, ct); staff.LinkUser(input.UserId, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<StaffSensitiveInfo?> GetSensitiveAsync(Guid actor, Guid id, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffSensitiveView, ct); await EnsureStaffAsync(actor, id, ct); return await db.StaffSensitiveRecords.AsNoTracking().Where(x => x.StaffId == id).Select(x => new StaffSensitiveInfo(x.Address, x.NextOfKinName, x.NextOfKinPhone, x.Notes, x.Title, x.MiddleName, x.Gender, x.DateOfBirth, x.MaritalStatus, x.Religion, x.Country, x.State, x.LocalGovernment, x.City, x.Genotype, x.BloodGroup, x.WeightKg, x.HeightCm, x.Disability, x.Skills, x.Achievements, x.Website, x.OfficeAddress, x.SocialProfilesJson, x.UpdatedAtUtc)).SingleOrDefaultAsync(ct); }

    public async Task UpsertSensitiveAsync(Guid actor, Guid id, StaffSensitiveInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, id, ct); var record = await db.StaffSensitiveRecords.SingleOrDefaultAsync(x => x.StaffId == id, ct); if (record is null) db.StaffSensitiveRecords.Add(new StaffSensitiveRecord(RequireTenant(), id, input.Address, input.NextOfKinName, input.NextOfKinPhone, input.Notes, input.Title, input.MiddleName, input.Gender, input.DateOfBirth, input.MaritalStatus, input.Religion, input.Country, input.State, input.LocalGovernment, input.City, input.Genotype, input.BloodGroup, input.WeightKg, input.HeightCm, input.Disability, input.Skills, input.Achievements, input.Website, input.OfficeAddress, input.SocialProfilesJson, clock.UtcNow)); else record.Update(input.Address, input.NextOfKinName, input.NextOfKinPhone, input.Notes, input.Title, input.MiddleName, input.Gender, input.DateOfBirth, input.MaritalStatus, input.Religion, input.Country, input.State, input.LocalGovernment, input.City, input.Genotype, input.BloodGroup, input.WeightKg, input.HeightCm, input.Disability, input.Skills, input.Achievements, input.Website, input.OfficeAddress, input.SocialProfilesJson, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<StaffEmploymentInfo>> ListEmploymentAsync(Guid actor, Guid staffId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); await EnsureStaffAsync(actor, staffId, ct); return await db.StaffEmploymentRecords.AsNoTracking().Where(x => x.StaffId == staffId).OrderByDescending(x => x.StartedOn).Select(x => new StaffEmploymentInfo(x.Id, x.EmployerName, x.JobTitle, x.StartedOn, x.EndedOn, x.ReasonForLeaving)).ToListAsync(ct); }

    public async Task<Guid> AddEmploymentAsync(Guid actor, Guid staffId, StaffEmploymentInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var id = Guid.NewGuid(); db.StaffEmploymentRecords.Add(new StaffEmploymentRecord(id, RequireTenant(), staffId, input.EmployerName, input.JobTitle, input.StartedOn, input.EndedOn, input.ReasonForLeaving, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task DeleteEmploymentAsync(Guid actor, Guid staffId, Guid recordId, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var record = await db.StaffEmploymentRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.StaffId == staffId, ct) ?? throw new KeyNotFoundException("Employment record was not found."); db.Remove(record); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<StaffQualificationInfo>> ListQualificationsAsync(Guid actor, Guid staffId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); await EnsureStaffAsync(actor, staffId, ct); return await db.StaffQualifications.AsNoTracking().Where(x => x.StaffId == staffId).OrderByDescending(x => x.AwardedOn).Select(x => new StaffQualificationInfo(x.Id, x.Institution, x.Name, x.FieldOfStudy, x.AwardedOn, x.Grade)).ToListAsync(ct); }

    public async Task<Guid> AddQualificationAsync(Guid actor, Guid staffId, StaffQualificationInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var id = Guid.NewGuid(); db.StaffQualifications.Add(new StaffQualification(id, RequireTenant(), staffId, input.Institution, input.Name, input.FieldOfStudy, input.AwardedOn, input.Grade, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task DeleteQualificationAsync(Guid actor, Guid staffId, Guid qualificationId, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var record = await db.StaffQualifications.SingleOrDefaultAsync(x => x.Id == qualificationId && x.StaffId == staffId, ct) ?? throw new KeyNotFoundException("Qualification was not found."); db.Remove(record); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<StaffNextOfKinInfo>> ListNextOfKinAsync(Guid actor, Guid staffId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffSensitiveView, ct); await EnsureStaffAsync(actor, staffId, ct); return await db.StaffNextOfKinContacts.AsNoTracking().Where(x => x.StaffId == staffId).OrderByDescending(x => x.IsPrimary).ThenBy(x => x.FullName).Select(x => new StaffNextOfKinInfo(x.Id, x.FullName, x.Relationship, x.Phone, x.Email, x.Address, x.IsPrimary)).ToListAsync(ct); }

    public async Task<Guid> AddNextOfKinAsync(Guid actor, Guid staffId, StaffNextOfKinInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); if (input.IsPrimary) await ClearPrimaryAsync(staffId, null, ct); var id = Guid.NewGuid(); db.StaffNextOfKinContacts.Add(new StaffNextOfKin(id, RequireTenant(), staffId, input.FullName, input.Relationship, input.Phone, input.Email, input.Address, input.IsPrimary, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task UpdateNextOfKinAsync(Guid actor, Guid staffId, Guid contactId, StaffNextOfKinInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var contact = await db.StaffNextOfKinContacts.SingleOrDefaultAsync(x => x.Id == contactId && x.StaffId == staffId, ct) ?? throw new KeyNotFoundException("Next-of-kin contact was not found."); if (input.IsPrimary) await ClearPrimaryAsync(staffId, contactId, ct); contact.Update(input.FullName, input.Relationship, input.Phone, input.Email, input.Address, input.IsPrimary, clock.UtcNow); await db.SaveChangesAsync(ct); }

    public async Task DeleteNextOfKinAsync(Guid actor, Guid staffId, Guid contactId, CancellationToken ct = default)
    { await ManageAsync(actor, ct); await EnsureStaffAsync(actor, staffId, ct); var contact = await db.StaffNextOfKinContacts.SingleOrDefaultAsync(x => x.Id == contactId && x.StaffId == staffId, ct) ?? throw new KeyNotFoundException("Next-of-kin contact was not found."); db.Remove(contact); await db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<TeachingAssignmentInfo>> ListTeachingAssignmentsAsync(Guid actor, Guid? classSectionId, CancellationToken ct = default)
    { await DemandAsync(actor, Permissions.StaffView, ct); RequireTenant(); var query = db.TeachingAssignments.AsNoTracking().Where(x => !classSectionId.HasValue || x.ClassSectionId == classSectionId); if (!await permissions.HasPermissionAsync(actor, Permissions.StaffManage, ct)) query = query.Where(x => db.StaffProfiles.Any(staff => staff.Id == x.StaffId && staff.UserId == actor)); return await query.OrderBy(x => x.ClassSectionId).ThenBy(x => x.Role).Select(x => new TeachingAssignmentInfo(x.Id, x.StaffId, x.ClassSectionId, x.SubjectId, x.Role)).ToListAsync(ct); }

    public async Task<Guid> CreateTeachingAssignmentAsync(Guid actor, TeachingAssignmentInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        if (!await db.StaffProfiles.AnyAsync(x => x.Id == input.StaffId && x.Category == StaffCategory.Teaching && x.Status == StaffStatus.Active, ct)) throw new InvalidOperationException("The assigned staff member must be active teaching staff in the current tenant.");
        if (!await db.ClassSections.AnyAsync(x => x.Id == input.ClassSectionId && x.IsActive, ct)) throw new InvalidOperationException("The class section must belong to the current tenant and be active.");
        if (input.Role == TeachingAssignmentRole.SubjectTeacher && (!input.SubjectId.HasValue || !await db.ClassSubjects.AnyAsync(x => x.ClassSectionId == input.ClassSectionId && x.SubjectId == input.SubjectId, ct))) throw new InvalidOperationException("The subject must already be assigned to the class section.");
        var id = Guid.NewGuid(); db.TeachingAssignments.Add(new TeachingAssignment(id, RequireTenant(), input.StaffId, input.ClassSectionId, input.SubjectId, input.Role, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task DeleteTeachingAssignmentAsync(Guid actor, Guid assignmentId, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var assignment = await db.TeachingAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw new KeyNotFoundException("Teaching assignment was not found."); db.TeachingAssignments.Remove(assignment); await db.SaveChangesAsync(ct); }

    private async Task ValidateReferencesAsync(StaffInput input, Guid campusId, CancellationToken ct)
    {
        if (!await db.Campuses.AnyAsync(x => x.Id == campusId && x.IsActive, ct)) throw new InvalidOperationException("The active campus workspace must belong to the current tenant and be active.");
        if (input.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == input.DepartmentId && x.IsActive, ct)) throw new InvalidOperationException("Department must belong to the current tenant and be active.");
        if (input.PositionId.HasValue && !await db.Positions.AnyAsync(x => x.Id == input.PositionId && x.IsActive && x.Category == input.Category, ct)) throw new InvalidOperationException("Position must belong to the current tenant, be active, and match the staff category.");
    }
    private static void ValidateContact(StaffInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Phone) || input.Phone.Length != 11 || input.Phone.Any(character => !char.IsAsciiDigit(character)))
            throw new ArgumentException("Phone number must contain exactly 11 digits.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.Email) || !System.Net.Mail.MailAddress.TryCreate(input.Email, out _))
            throw new ArgumentException("Enter a valid email address.", nameof(input));
    }
    private async Task EnsureUniqueContactAsync(string email, string phone, Guid? exceptStaffId, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await db.StaffProfiles.AnyAsync(x => (!exceptStaffId.HasValue || x.Id != exceptStaffId) && x.WorkEmail == normalizedEmail, ct))
            throw new InvalidOperationException("A staff record already exists with this email address.");
        if (await db.StaffProfiles.AnyAsync(x => (!exceptStaffId.HasValue || x.Id != exceptStaffId) && x.Phone == phone, ct))
            throw new InvalidOperationException("A staff record already exists with this phone number.");
    }
    private async Task<Guid> ResolveCampusAsync(Guid requestedCampusId, Guid? existingCampusId, CancellationToken ct)
    {
        var campusId = tenant.CampusId ?? existingCampusId ?? (requestedCampusId == Guid.Empty ? null : requestedCampusId);
        if (!campusId.HasValue || campusId == Guid.Empty)
            throw new InvalidOperationException("Select an active campus workspace before creating or updating a staff record.");
        if (!await db.Campuses.AnyAsync(x => x.Id == campusId && x.IsActive, ct))
            throw new InvalidOperationException("The active campus workspace is unavailable.");
        return campusId.Value;
    }
    private async Task<string> GenerateStaffNumberAsync(CancellationToken ct)
    {
        var year = clock.UtcNow.Year;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = $"STF-{year}-{Guid.NewGuid():N}"[..17].ToUpperInvariant();
            if (!await db.StaffProfiles.AnyAsync(x => x.StaffNumber == candidate, ct)) return candidate;
        }
        throw new InvalidOperationException("A unique staff number could not be generated. Try again.");
    }
    private async Task<StaffProfile> FindAsync(Guid id, CancellationToken ct) => await db.StaffProfiles.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Staff member was not found.");
    private async Task EnsureStaffAsync(Guid actor, Guid id, CancellationToken ct) { RequireTenant(); var query = db.StaffProfiles.Where(x => x.Id == id); if (!await permissions.HasPermissionAsync(actor, Permissions.StaffManage, ct)) query = query.Where(x => x.UserId == actor); if (!await query.AnyAsync(ct)) throw new KeyNotFoundException("Staff member was not found."); }
    private async Task ClearPrimaryAsync(Guid staffId, Guid? exceptId, CancellationToken ct) { var contacts = await db.StaffNextOfKinContacts.Where(x => x.StaffId == staffId && x.IsPrimary && (!exceptId.HasValue || x.Id != exceptId)).ToListAsync(ct); foreach (var contact in contacts) contact.RemovePrimary(clock.UtcNow); }
    private async Task<string> GeneratePositionCodeAsync(string name, CancellationToken ct)
    {
        var baseCode = new string(string.Join('-', name.ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Select(character => char.IsAsciiLetterOrDigit(character) || character == '-' ? character : '-').ToArray());
        while (baseCode.Contains("--", StringComparison.Ordinal)) baseCode = baseCode.Replace("--", "-", StringComparison.Ordinal);
        baseCode = baseCode.Trim('-');
        if (string.IsNullOrWhiteSpace(baseCode)) throw new ArgumentException("Position name must contain letters or numbers.", nameof(name));
        baseCode = baseCode[..Math.Min(baseCode.Length, 40)].TrimEnd('-');
        var candidate = baseCode;
        for (var suffix = 2; await db.Positions.AnyAsync(x => x.Code == candidate, ct); suffix++)
            candidate = $"{baseCode[..Math.Min(baseCode.Length, 40 - suffix.ToString().Length - 1)]}-{suffix}";
        return candidate;
    }
    private static string ValidatePositionName(PositionInput input)
    {
        var name = string.Join(' ', (input.Name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return name.Length is 0 or > 150
            ? throw new ArgumentException("Position name is required and must not exceed 150 characters.", nameof(input))
            : name;
    }
    private async Task EnsureDefaultPositionsAsync(CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var defaults = new (StaffCategory Category, string Name)[] {
            (StaffCategory.Teaching,"Principal"),(StaffCategory.Teaching,"Vice Principal Academics"),(StaffCategory.Teaching,"Head Teacher"),(StaffCategory.Teaching,"Deputy Head Teacher"),(StaffCategory.Teaching,"Head of Department"),(StaffCategory.Teaching,"Class Teacher"),(StaffCategory.Teaching,"Subject Teacher"),(StaffCategory.Teaching,"Teaching Assistant"),(StaffCategory.Teaching,"Special Education Teacher"),(StaffCategory.Teaching,"Guidance Counsellor"),(StaffCategory.Teaching,"Librarian"),(StaffCategory.Teaching,"Laboratory Technician"),
            (StaffCategory.Administrative,"School Administrator"),(StaffCategory.Administrative,"Administrative Officer"),(StaffCategory.Administrative,"Registrar"),(StaffCategory.Administrative,"Admissions Officer"),(StaffCategory.Administrative,"Bursar"),(StaffCategory.Administrative,"Accountant"),(StaffCategory.Administrative,"Finance Officer"),(StaffCategory.Administrative,"Human Resources Officer"),(StaffCategory.Administrative,"ICT Officer"),(StaffCategory.Administrative,"Receptionist"),(StaffCategory.Administrative,"Storekeeper"),
            (StaffCategory.NonTeaching,"School Nurse"),(StaffCategory.NonTeaching,"Welfare Officer"),(StaffCategory.NonTeaching,"Boarding House Parent"),(StaffCategory.NonTeaching,"Driver"),(StaffCategory.NonTeaching,"Security Officer"),(StaffCategory.NonTeaching,"Cleaner"),(StaffCategory.NonTeaching,"Maintenance Officer"),(StaffCategory.NonTeaching,"Cook") };
        var existing = await db.Positions.Select(x => new { x.Category, x.Name }).ToListAsync(ct);
        foreach (var item in defaults.Where(item => !existing.Any(value => value.Category == item.Category && value.Name == item.Name)))
            db.Positions.Add(new Position(Guid.NewGuid(), tenantId, item.Name, await GeneratePositionCodeAsync(item.Name, ct), item.Category, false, clock.UtcNow));
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
    }
    private Task ManageAsync(Guid actor, CancellationToken ct) => DemandAsync(actor, Permissions.StaffManage, ct);
    private Task DemandAsync(Guid actor, string permission, CancellationToken ct) => access.DemandAsync(actor, permission, FeatureKeys.StaffManagement, ct);
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    private static System.Linq.Expressions.Expression<Func<StaffProfile, StaffInfo>> Project() => x => new StaffInfo(x.Id, x.UserId, x.StaffNumber, x.FirstName, x.LastName, x.Category, x.Status, x.CampusId, x.DepartmentId, x.PositionId, x.WorkEmail, x.Phone, x.HireDate, x.ExitDate);
}
