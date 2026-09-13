using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Academics.Domain;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Academics;

public sealed record AcademicYearInput(string Name, DateOnly StartsOn, DateOnly EndsOn);
public sealed record AcademicTermInput(Guid AcademicYearId, string Name, string Code, int Sequence, DateOnly StartsOn, DateOnly EndsOn);
public sealed record EducationStageInput(string Name, string Code, int DisplayOrder);
public sealed record ClassLevelInput(Guid EducationStageId, string Name, string Code, int DisplayOrder);
public sealed record ClassSectionInput(Guid CampusId, Guid AcademicYearId, Guid ClassLevelId, string Name, string Code, int? Capacity);
public sealed record DepartmentInput(string Name, string Code);
public sealed record SubjectInput(Guid? DepartmentId, string Name, string Code, bool IsCore);
public sealed record ClassSubjectInput(Guid ClassSectionId, Guid SubjectId, bool IsCompulsory);
public sealed record AcademicYearInfo(Guid Id, string Name, DateOnly StartsOn, DateOnly EndsOn, AcademicPeriodStatus Status);
public sealed record AcademicTermInfo(Guid Id, Guid AcademicYearId, string Name, string Code, int Sequence, DateOnly StartsOn, DateOnly EndsOn, AcademicPeriodStatus Status);
public sealed record EducationStageInfo(Guid Id, string Name, string Code, int DisplayOrder, bool IsActive);
public sealed record ClassLevelInfo(Guid Id, Guid EducationStageId, string Name, string Code, int DisplayOrder, bool IsActive);
public sealed record ClassSectionInfo(Guid Id, Guid CampusId, Guid AcademicYearId, Guid ClassLevelId, string Name, string Code, int? Capacity, bool IsActive);
public sealed record DepartmentInfo(Guid Id, string Name, string Code, bool IsActive);
public sealed record SubjectInfo(Guid Id, Guid? DepartmentId, string Name, string Code, bool IsCore, bool IsActive);
public sealed record ClassSubjectInfo(Guid ClassSectionId, Guid SubjectId, bool IsCompulsory);
public sealed record AcademicStructureInfo(IReadOnlyCollection<AcademicYearInfo> AcademicYears, IReadOnlyCollection<AcademicTermInfo> Terms,
    IReadOnlyCollection<EducationStageInfo> EducationStages, IReadOnlyCollection<ClassLevelInfo> ClassLevels, IReadOnlyCollection<ClassSectionInfo> ClassSections,
    IReadOnlyCollection<DepartmentInfo> Departments, IReadOnlyCollection<SubjectInfo> Subjects, IReadOnlyCollection<ClassSubjectInfo> ClassSubjects);

public interface IAcademicStructureService
{
    Task<AcademicStructureInfo> GetAsync(Guid actorUserId, Guid? academicYearId = null, CancellationToken cancellationToken = default);
    Task<Guid> CreateAcademicYearAsync(Guid actorUserId, AcademicYearInput input, CancellationToken cancellationToken = default);
    Task ActivateAcademicYearAsync(Guid actorUserId, Guid academicYearId, CancellationToken cancellationToken = default);
    Task<Guid> CreateTermAsync(Guid actorUserId, AcademicTermInput input, CancellationToken cancellationToken = default);
    Task<Guid> CreateEducationStageAsync(Guid actorUserId, EducationStageInput input, CancellationToken cancellationToken = default);
    Task<Guid> CreateClassLevelAsync(Guid actorUserId, ClassLevelInput input, CancellationToken cancellationToken = default);
    Task<Guid> CreateClassSectionAsync(Guid actorUserId, ClassSectionInput input, CancellationToken cancellationToken = default);
    Task<Guid> CreateDepartmentAsync(Guid actorUserId, DepartmentInput input, CancellationToken cancellationToken = default);
    Task<Guid> CreateSubjectAsync(Guid actorUserId, SubjectInput input, CancellationToken cancellationToken = default);
    Task AssignSubjectAsync(Guid actorUserId, ClassSubjectInput input, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid actorUserId, string resource, Guid id, object input, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid actorUserId, string resource, Guid id, Guid? relatedId = null, CancellationToken cancellationToken = default);
}

public sealed class AcademicStructureService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IClock clock) : IAcademicStructureService
{
    public async Task<AcademicStructureInfo> GetAsync(Guid actor, Guid? academicYearId = null, CancellationToken ct = default)
    {
        await DemandAsync(actor, Permissions.AcademicsView, ct); RequireTenant();
        var years = await db.AcademicYears.AsNoTracking().OrderByDescending(x => x.StartsOn).Select(x => new AcademicYearInfo(x.Id, x.Name, x.StartsOn, x.EndsOn, x.Status)).ToListAsync(ct);
        var terms = await db.AcademicTerms.AsNoTracking().Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId).OrderBy(x => x.Sequence).Select(x => new AcademicTermInfo(x.Id, x.AcademicYearId, x.Name, x.Code, x.Sequence, x.StartsOn, x.EndsOn, x.Status)).ToListAsync(ct);
        var sections = await db.ClassSections.AsNoTracking().Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId).OrderBy(x => x.Name).Select(x => new ClassSectionInfo(x.Id, x.CampusId, x.AcademicYearId, x.ClassLevelId, x.Name, x.Code, x.Capacity, x.IsActive)).ToListAsync(ct);
        return new(years, terms, await db.EducationStages.AsNoTracking().OrderBy(x => x.DisplayOrder).Select(x => new EducationStageInfo(x.Id, x.Name, x.Code, x.DisplayOrder, x.IsActive)).ToListAsync(ct),
            await db.ClassLevels.AsNoTracking().OrderBy(x => x.DisplayOrder).Select(x => new ClassLevelInfo(x.Id, x.EducationStageId, x.Name, x.Code, x.DisplayOrder, x.IsActive)).ToListAsync(ct), sections,
            await db.Departments.AsNoTracking().OrderBy(x => x.Name).Select(x => new DepartmentInfo(x.Id, x.Name, x.Code, x.IsActive)).ToListAsync(ct),
            await db.Subjects.AsNoTracking().OrderBy(x => x.Name).Select(x => new SubjectInfo(x.Id, x.DepartmentId, x.Name, x.Code, x.IsCore, x.IsActive)).ToListAsync(ct),
            await db.ClassSubjects.AsNoTracking().Select(x => new ClassSubjectInfo(x.ClassSectionId, x.SubjectId, x.IsCompulsory)).ToListAsync(ct));
    }

    public async Task<Guid> CreateAcademicYearAsync(Guid actor, AcademicYearInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var id = Guid.NewGuid(); db.AcademicYears.Add(new AcademicYear(id, RequireTenant(), input.Name, input.StartsOn, input.EndsOn, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task ActivateAcademicYearAsync(Guid actor, Guid academicYearId, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct); var year = await db.AcademicYears.SingleOrDefaultAsync(x => x.Id == academicYearId, ct) ?? throw new KeyNotFoundException("Academic year was not found.");
        if (await db.AcademicYears.AnyAsync(x => x.Id != academicYearId && x.Status == AcademicPeriodStatus.Active, ct)) throw new InvalidOperationException("Close the current academic year before activating another one.");
        year.Activate(clock.UtcNow); await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> CreateTermAsync(Guid actor, AcademicTermInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct); var year = await db.AcademicYears.SingleOrDefaultAsync(x => x.Id == input.AcademicYearId, ct) ?? throw new KeyNotFoundException("Academic year was not found.");
        if (input.StartsOn < year.StartsOn || input.EndsOn > year.EndsOn) throw new ArgumentException("Term dates must fall within the academic year.", nameof(input));
        if (await db.AcademicTerms.AnyAsync(x => x.AcademicYearId == input.AcademicYearId && x.StartsOn <= input.EndsOn && x.EndsOn >= input.StartsOn, ct)) throw new InvalidOperationException("Term dates cannot overlap.");
        var id = Guid.NewGuid(); db.AcademicTerms.Add(new AcademicTerm(id, RequireTenant(), input.AcademicYearId, input.Name, input.Code, input.Sequence, input.StartsOn, input.EndsOn, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task<Guid> CreateEducationStageAsync(Guid actor, EducationStageInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var id = Guid.NewGuid(); db.EducationStages.Add(new EducationStage(id, RequireTenant(), input.Name, input.Code, input.DisplayOrder, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task<Guid> CreateClassLevelAsync(Guid actor, ClassLevelInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); if (!await db.EducationStages.AnyAsync(x => x.Id == input.EducationStageId && x.IsActive, ct)) throw new KeyNotFoundException("Education stage was not found."); var id = Guid.NewGuid(); db.ClassLevels.Add(new ClassLevel(id, RequireTenant(), input.EducationStageId, input.Name, input.Code, input.DisplayOrder, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task<Guid> CreateClassSectionAsync(Guid actor, ClassSectionInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        if (!await db.Campuses.AnyAsync(x => x.Id == input.CampusId && x.IsActive, ct) || !await db.AcademicYears.AnyAsync(x => x.Id == input.AcademicYearId, ct) || !await db.ClassLevels.AnyAsync(x => x.Id == input.ClassLevelId && x.IsActive, ct))
            throw new InvalidOperationException("Campus, academic year, and class level must belong to the current tenant and be active.");
        var id = Guid.NewGuid(); db.ClassSections.Add(new ClassSection(id, RequireTenant(), input.CampusId, input.AcademicYearId, input.ClassLevelId, input.Name, input.Code, input.Capacity, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task<Guid> CreateDepartmentAsync(Guid actor, DepartmentInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var id = Guid.NewGuid(); db.Departments.Add(new Department(id, RequireTenant(), input.Name, input.Code, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task<Guid> CreateSubjectAsync(Guid actor, SubjectInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); if (input.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == input.DepartmentId && x.IsActive, ct)) throw new KeyNotFoundException("Department was not found."); var id = Guid.NewGuid(); db.Subjects.Add(new Subject(id, RequireTenant(), input.DepartmentId, input.Name, input.Code, input.IsCore, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task AssignSubjectAsync(Guid actor, ClassSubjectInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); if (!await db.ClassSections.AnyAsync(x => x.Id == input.ClassSectionId && x.IsActive, ct) || !await db.Subjects.AnyAsync(x => x.Id == input.SubjectId && x.IsActive, ct)) throw new InvalidOperationException("Class section and subject must belong to the current tenant and be active."); var existing = await db.ClassSubjects.SingleOrDefaultAsync(x => x.ClassSectionId == input.ClassSectionId && x.SubjectId == input.SubjectId, ct); if (existing is null) db.ClassSubjects.Add(new ClassSubject(RequireTenant(), input.ClassSectionId, input.SubjectId, input.IsCompulsory)); else existing.Update(input.IsCompulsory); await db.SaveChangesAsync(ct); }

    public async Task UpdateAsync(Guid actor, string resource, Guid id, object input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        switch (resource)
        {
            case "years": { var value = (AcademicYearInput)input; var entity = await db.AcademicYears.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); if (await db.AcademicTerms.AnyAsync(x => x.AcademicYearId == id && (x.StartsOn < value.StartsOn || x.EndsOn > value.EndsOn), ct)) throw new InvalidOperationException("Existing term dates must remain within the academic year."); entity.Update(value.Name, value.StartsOn, value.EndsOn, clock.UtcNow); break; }
            case "terms": { var value = (AcademicTermInput)input; var entity = await db.AcademicTerms.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); await ValidateTermAsync(value, id, ct); entity.Update(value.AcademicYearId, value.Name, value.Code, value.Sequence, value.StartsOn, value.EndsOn); break; }
            case "education-stages": { var value = (EducationStageInput)input; var entity = await db.EducationStages.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); entity.Update(value.Name, value.Code, value.DisplayOrder); break; }
            case "class-levels": { var value = (ClassLevelInput)input; if (!await db.EducationStages.AnyAsync(x => x.Id == value.EducationStageId, ct)) throw Missing(); var entity = await db.ClassLevels.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); entity.Update(value.EducationStageId, value.Name, value.Code, value.DisplayOrder); break; }
            case "class-sections": { var value = (ClassSectionInput)input; if (!await db.Campuses.AnyAsync(x => x.Id == value.CampusId, ct) || !await db.AcademicYears.AnyAsync(x => x.Id == value.AcademicYearId, ct) || !await db.ClassLevels.AnyAsync(x => x.Id == value.ClassLevelId, ct)) throw Missing(); var entity = await db.ClassSections.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); entity.Update(value.CampusId, value.AcademicYearId, value.ClassLevelId, value.Name, value.Code, value.Capacity); break; }
            case "departments": { var value = (DepartmentInput)input; var entity = await db.Departments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); entity.Update(value.Name, value.Code); break; }
            case "subjects": { var value = (SubjectInput)input; if (value.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == value.DepartmentId, ct)) throw Missing(); var entity = await db.Subjects.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); entity.Update(value.DepartmentId, value.Name, value.Code, value.IsCore); break; }
            default: throw new ArgumentException("Unsupported academic resource.", nameof(resource));
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid actor, string resource, Guid id, Guid? relatedId = null, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var affected = resource switch
        {
            "years" when !await db.AcademicTerms.AnyAsync(x => x.AcademicYearId == id, ct) && !await db.ClassSections.AnyAsync(x => x.AcademicYearId == id, ct) => await db.AcademicYears.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "terms" => await db.AcademicTerms.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "education-stages" when !await db.ClassLevels.AnyAsync(x => x.EducationStageId == id, ct) => await db.EducationStages.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "class-levels" when !await db.ClassSections.AnyAsync(x => x.ClassLevelId == id, ct) => await db.ClassLevels.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "class-sections" when !await db.Enrollments.AnyAsync(x => x.ClassSectionId == id, ct) => await db.ClassSections.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "departments" when !await db.Subjects.AnyAsync(x => x.DepartmentId == id, ct) && !await db.StaffProfiles.AnyAsync(x => x.DepartmentId == id, ct) => await db.Departments.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "subjects" when !await db.ClassSubjects.AnyAsync(x => x.SubjectId == id, ct) && !await db.TeachingAssignments.AnyAsync(x => x.SubjectId == id, ct) => await db.Subjects.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "class-subjects" when relatedId.HasValue => await db.ClassSubjects.Where(x => x.ClassSectionId == id && x.SubjectId == relatedId.Value).ExecuteDeleteAsync(ct),
            _ => throw new InvalidOperationException("This item is in use. Remove its dependent records before deleting it.")
        };
        if (affected == 0) throw new KeyNotFoundException("Academic structure item was not found.");
    }

    private async Task ValidateTermAsync(AcademicTermInput input, Guid? exceptId, CancellationToken ct)
    {
        var year = await db.AcademicYears.SingleOrDefaultAsync(x => x.Id == input.AcademicYearId, ct) ?? throw Missing();
        if (input.StartsOn < year.StartsOn || input.EndsOn > year.EndsOn) throw new ArgumentException("Term dates must fall within the academic year.");
        if (await db.AcademicTerms.AnyAsync(x => x.Id != exceptId && x.AcademicYearId == input.AcademicYearId && x.StartsOn <= input.EndsOn && x.EndsOn >= input.StartsOn, ct)) throw new InvalidOperationException("Term dates cannot overlap.");
    }
    private static KeyNotFoundException Missing() => new("Academic structure item was not found.");

    private Task ManageAsync(Guid actor, CancellationToken ct) => DemandAsync(actor, Permissions.AcademicsManage, ct);
    private Task DemandAsync(Guid actor, string permission, CancellationToken ct) => access.DemandAsync(actor, permission, FeatureKeys.AcademicStructure, ct);
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
}
