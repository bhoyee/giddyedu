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
    { await ManageAsync(actor, ct); if (!await db.ClassSections.AnyAsync(x => x.Id == input.ClassSectionId && x.IsActive, ct) || !await db.Subjects.AnyAsync(x => x.Id == input.SubjectId && x.IsActive, ct)) throw new InvalidOperationException("Class section and subject must belong to the current tenant and be active."); if (await db.ClassSubjects.AnyAsync(x => x.ClassSectionId == input.ClassSectionId && x.SubjectId == input.SubjectId, ct)) return; db.ClassSubjects.Add(new ClassSubject(RequireTenant(), input.ClassSectionId, input.SubjectId, input.IsCompulsory)); await db.SaveChangesAsync(ct); }

    private Task ManageAsync(Guid actor, CancellationToken ct) => DemandAsync(actor, Permissions.AcademicsManage, ct);
    private Task DemandAsync(Guid actor, string permission, CancellationToken ct) => access.DemandAsync(actor, permission, FeatureKeys.AcademicStructure, ct);
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
}
