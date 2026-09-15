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
public sealed record EducationStageInput(string Name, string? Code = null, int? DisplayOrder = null);
public sealed record ClassLevelInput(Guid EducationStageId, string Name, string? Code = null, int? DisplayOrder = null);
public sealed record ClassSectionInput(Guid ClassLevelId, string Name, int? Capacity = null, Guid? CampusId = null, Guid? AcademicYearId = null, string? Code = null);
public sealed record DepartmentInput(string Name, string? Code = null);
public sealed record SubjectInput(Guid? DepartmentId, string Name, bool IsCore, string? Code = null);
public sealed record ClassSubjectInput(Guid ClassSectionId, Guid SubjectId, bool IsCompulsory);
public sealed record AcademicYearInfo(Guid Id, string Name, DateOnly StartsOn, DateOnly EndsOn, AcademicPeriodStatus Status, string StatusLabel);
public sealed record AcademicTermInfo(Guid Id, Guid AcademicYearId, string Name, string Code, int Sequence, DateOnly StartsOn, DateOnly EndsOn, AcademicPeriodStatus Status, string StatusLabel);
public sealed record EducationStageInfo(Guid Id, string Name, string Code, int DisplayOrder, bool IsActive);
public sealed record ClassLevelInfo(Guid Id, Guid EducationStageId, string EducationStageName, string Name, string Code, int DisplayOrder, bool IsActive);
public sealed record ClassSectionInfo(Guid Id, Guid CampusId, string CampusName, Guid AcademicYearId, string AcademicYearName, Guid ClassLevelId, string ClassLevelName, string Name, string Code, int? Capacity, bool IsActive);
public sealed record DepartmentInfo(Guid Id, string Name, string Code, bool IsActive);
public sealed record SubjectInfo(Guid Id, Guid? DepartmentId, string Name, string Code, bool IsCore, bool IsActive);
public sealed record ClassSubjectInfo(Guid ClassSectionId, string ClassSectionName, Guid SubjectId, string SubjectName, bool IsCompulsory, string Requirement);
public sealed record AcademicStructureInfo(IReadOnlyCollection<AcademicYearInfo> AcademicYears, IReadOnlyCollection<AcademicTermInfo> Terms,
    IReadOnlyCollection<EducationStageInfo> EducationStages, IReadOnlyCollection<ClassLevelInfo> ClassLevels, IReadOnlyCollection<ClassSectionInfo> ClassSections,
    IReadOnlyCollection<DepartmentInfo> Departments, IReadOnlyCollection<SubjectInfo> Subjects, IReadOnlyCollection<ClassSubjectInfo> ClassSubjects);

public interface IAcademicStructureService
{
    Task<AcademicStructureInfo> GetAsync(Guid actorUserId, Guid? academicYearId = null, CancellationToken cancellationToken = default);
    Task<Guid> CreateAcademicYearAsync(Guid actorUserId, AcademicYearInput input, CancellationToken cancellationToken = default);
    Task ActivateAcademicYearAsync(Guid actorUserId, Guid academicYearId, CancellationToken cancellationToken = default);
    Task ActivateTermAsync(Guid actorUserId, Guid termId, CancellationToken cancellationToken = default);
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
        var years = await db.AcademicYears.AsNoTracking().OrderByDescending(x => x.StartsOn).Select(x => new AcademicYearInfo(x.Id, x.Name, x.StartsOn, x.EndsOn, x.Status, x.Status == AcademicPeriodStatus.Planned ? "Planned" : x.Status == AcademicPeriodStatus.Active ? "Active" : "Closed")).ToListAsync(ct);
        var terms = await db.AcademicTerms.AsNoTracking().Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId).OrderBy(x => x.Sequence).Select(x => new AcademicTermInfo(x.Id, x.AcademicYearId, x.Name, x.Code, x.Sequence, x.StartsOn, x.EndsOn, x.Status, x.Status == AcademicPeriodStatus.Planned ? "Planned" : x.Status == AcademicPeriodStatus.Active ? "Active" : "Closed")).ToListAsync(ct);
        var sections = await (from section in db.ClassSections.AsNoTracking() join campus in db.Campuses.AsNoTracking() on section.CampusId equals campus.Id join year in db.AcademicYears.AsNoTracking() on section.AcademicYearId equals year.Id join level in db.ClassLevels.AsNoTracking() on section.ClassLevelId equals level.Id where !academicYearId.HasValue || section.AcademicYearId == academicYearId orderby level.DisplayOrder, section.Name select new ClassSectionInfo(section.Id, section.CampusId, campus.Name, section.AcademicYearId, year.Name, section.ClassLevelId, level.Name, section.Name, section.Code, section.Capacity, section.IsActive)).ToListAsync(ct);
        return new(years, terms, await db.EducationStages.AsNoTracking().OrderBy(x => x.DisplayOrder).Select(x => new EducationStageInfo(x.Id, x.Name, x.Code, x.DisplayOrder, x.IsActive)).ToListAsync(ct),
            await (from level in db.ClassLevels.AsNoTracking() join stage in db.EducationStages.AsNoTracking() on level.EducationStageId equals stage.Id orderby stage.DisplayOrder, level.DisplayOrder select new ClassLevelInfo(level.Id, level.EducationStageId, stage.Name, level.Name, level.Code, level.DisplayOrder, level.IsActive)).ToListAsync(ct), sections,
            await db.Departments.AsNoTracking().OrderBy(x => x.Name).Select(x => new DepartmentInfo(x.Id, x.Name, x.Code, x.IsActive)).ToListAsync(ct),
            await db.Subjects.AsNoTracking().OrderBy(x => x.Name).Select(x => new SubjectInfo(x.Id, x.DepartmentId, x.Name, x.Code, x.IsCore, x.IsActive)).ToListAsync(ct),
            await (from assignment in db.ClassSubjects.AsNoTracking() join section in db.ClassSections.AsNoTracking() on assignment.ClassSectionId equals section.Id join subject in db.Subjects.AsNoTracking() on assignment.SubjectId equals subject.Id orderby section.Name, subject.Name select new ClassSubjectInfo(assignment.ClassSectionId, section.Name, assignment.SubjectId, subject.Name, assignment.IsCompulsory, assignment.IsCompulsory ? "Compulsory" : "Elective")).ToListAsync(ct));
    }

    public async Task<Guid> CreateAcademicYearAsync(Guid actor, AcademicYearInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var name = ValidateAcademicYear(input); var id = Guid.NewGuid(); db.AcademicYears.Add(new AcademicYear(id, RequireTenant(), name, input.StartsOn, input.EndsOn, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task ActivateAcademicYearAsync(Guid actor, Guid academicYearId, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct); var year = await db.AcademicYears.SingleOrDefaultAsync(x => x.Id == academicYearId, ct) ?? throw new KeyNotFoundException("Academic year was not found.");
        if (await db.AcademicYears.AnyAsync(x => x.Id != academicYearId && x.Status == AcademicPeriodStatus.Active, ct)) throw new InvalidOperationException("Close the current academic year before activating another one.");
        year.Activate(clock.UtcNow); await db.SaveChangesAsync(ct);
    }

    public async Task ActivateTermAsync(Guid actor, Guid termId, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var term = await db.AcademicTerms.SingleOrDefaultAsync(x => x.Id == termId, ct) ?? throw Missing();
        if (!await db.AcademicYears.AnyAsync(x => x.Id == term.AcademicYearId && x.Status == AcademicPeriodStatus.Active, ct))
            throw new InvalidOperationException("Activate the term's academic year first.");
        var activeTerms = await db.AcademicTerms.Where(x => x.Id != termId && x.Status == AcademicPeriodStatus.Active).ToListAsync(ct);
        foreach (var activeTerm in activeTerms) activeTerm.Close();
        term.Activate();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> CreateTermAsync(Guid actor, AcademicTermInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct); var year = await db.AcademicYears.SingleOrDefaultAsync(x => x.Id == input.AcademicYearId, ct) ?? throw new KeyNotFoundException("Academic year was not found.");
        if (input.StartsOn < year.StartsOn || input.EndsOn > year.EndsOn) throw new ArgumentException("Term dates must fall within the academic year.", nameof(input));
        if (await db.AcademicTerms.AnyAsync(x => x.AcademicYearId == input.AcademicYearId && x.StartsOn <= input.EndsOn && x.EndsOn >= input.StartsOn, ct)) throw new InvalidOperationException("Term dates cannot overlap.");
        var id = Guid.NewGuid(); db.AcademicTerms.Add(new AcademicTerm(id, RequireTenant(), input.AcademicYearId, input.Name, input.Code, input.Sequence, input.StartsOn, input.EndsOn, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task<Guid> CreateEducationStageAsync(Guid actor, EducationStageInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var stage = StandardEducationStage.Resolve(input.Name); var id = Guid.NewGuid(); db.EducationStages.Add(new EducationStage(id, RequireTenant(), stage.Name, stage.Code, stage.DisplayOrder, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task<Guid> CreateClassLevelAsync(Guid actor, ClassLevelInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var stage = await db.EducationStages.SingleOrDefaultAsync(x => x.Id == input.EducationStageId && x.IsActive, ct) ?? throw new KeyNotFoundException("Education stage was not found."); var level = StandardClassLevel.Resolve(stage.Name, input.Name); var id = Guid.NewGuid(); db.ClassLevels.Add(new ClassLevel(id, RequireTenant(), input.EducationStageId, level.Name, level.Code, level.DisplayOrder, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task<Guid> CreateClassSectionAsync(Guid actor, ClassSectionInput input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        var campusId = await ResolveCurrentCampusAsync(ct);
        var yearId = await db.AcademicYears.Where(x => x.Status == AcademicPeriodStatus.Active).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("Set an active academic year before creating a class section.");
        var level = await db.ClassLevels.SingleOrDefaultAsync(x => x.Id == input.ClassLevelId && x.IsActive, ct) ?? throw new InvalidOperationException("Select an active class level from this school.");
        var identity = ClassSectionIdentity.Create(level.Name, level.Code, input.Name);
        var id = Guid.NewGuid(); db.ClassSections.Add(new ClassSection(id, RequireTenant(), campusId, yearId, input.ClassLevelId, identity.Name, identity.Code, input.Capacity, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task<Guid> CreateDepartmentAsync(Guid actor, DepartmentInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); var code = await UniqueCodeAsync(input.Name, value => db.Departments.AnyAsync(x => x.Code == value, ct)); var id = Guid.NewGuid(); db.Departments.Add(new Department(id, RequireTenant(), input.Name, code, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task<Guid> CreateSubjectAsync(Guid actor, SubjectInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); if (input.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == input.DepartmentId && x.IsActive, ct)) throw new KeyNotFoundException("Department was not found."); var code = await UniqueCodeAsync(input.Name, value => db.Subjects.AnyAsync(x => x.Code == value, ct)); var id = Guid.NewGuid(); db.Subjects.Add(new Subject(id, RequireTenant(), input.DepartmentId, input.Name, code, input.IsCore, clock.UtcNow)); await db.SaveChangesAsync(ct); return id; }

    public async Task AssignSubjectAsync(Guid actor, ClassSubjectInput input, CancellationToken ct = default)
    { await ManageAsync(actor, ct); if (!await db.ClassSections.AnyAsync(x => x.Id == input.ClassSectionId && x.IsActive, ct) || !await db.Subjects.AnyAsync(x => x.Id == input.SubjectId && x.IsActive, ct)) throw new InvalidOperationException("Class section and subject must belong to the current tenant and be active."); var existing = await db.ClassSubjects.SingleOrDefaultAsync(x => x.ClassSectionId == input.ClassSectionId && x.SubjectId == input.SubjectId, ct); if (existing is null) db.ClassSubjects.Add(new ClassSubject(RequireTenant(), input.ClassSectionId, input.SubjectId, input.IsCompulsory)); else existing.Update(input.IsCompulsory); await db.SaveChangesAsync(ct); }

    public async Task UpdateAsync(Guid actor, string resource, Guid id, object input, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        switch (resource)
        {
            case "years": { var value = (AcademicYearInput)input; var name = ValidateAcademicYear(value); var entity = await db.AcademicYears.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); if (await db.AcademicTerms.AnyAsync(x => x.AcademicYearId == id && (x.StartsOn < value.StartsOn || x.EndsOn > value.EndsOn), ct)) throw new InvalidOperationException("Existing term dates must remain within the academic year."); entity.Update(name, value.StartsOn, value.EndsOn, clock.UtcNow); break; }
            case "terms": { var value = (AcademicTermInput)input; var entity = await db.AcademicTerms.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); await ValidateTermAsync(value, id, ct); entity.Update(value.AcademicYearId, value.Name, value.Code, value.Sequence, value.StartsOn, value.EndsOn); break; }
            case "education-stages": { var value = (EducationStageInput)input; var stage = StandardEducationStage.Resolve(value.Name); var entity = await db.EducationStages.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); entity.Update(stage.Name, stage.Code, stage.DisplayOrder); break; }
            case "class-levels": { var value = (ClassLevelInput)input; var stage = await db.EducationStages.SingleOrDefaultAsync(x => x.Id == value.EducationStageId && x.IsActive, ct) ?? throw Missing(); var level = StandardClassLevel.Resolve(stage.Name, value.Name); var entity = await db.ClassLevels.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); entity.Update(value.EducationStageId, level.Name, level.Code, level.DisplayOrder); break; }
            case "class-sections": { var value = (ClassSectionInput)input; var level = await db.ClassLevels.SingleOrDefaultAsync(x => x.Id == value.ClassLevelId && x.IsActive, ct) ?? throw Missing(); var identity = ClassSectionIdentity.Create(level.Name, level.Code, value.Name); var entity = await db.ClassSections.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); entity.Update(entity.CampusId, entity.AcademicYearId, value.ClassLevelId, identity.Name, identity.Code, value.Capacity); break; }
            case "departments": { var value = (DepartmentInput)input; var entity = await db.Departments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); entity.Update(value.Name, entity.Code); break; }
            case "subjects": { var value = (SubjectInput)input; if (value.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == value.DepartmentId, ct)) throw Missing(); var entity = await db.Subjects.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(); entity.Update(value.DepartmentId, value.Name, entity.Code, value.IsCore); break; }
            default: throw new ArgumentException("Unsupported academic resource.", nameof(resource));
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid actor, string resource, Guid id, Guid? relatedId = null, CancellationToken ct = default)
    {
        await ManageAsync(actor, ct);
        if (resource == "class-subjects" && relatedId.HasValue)
        {
            var assignment = await db.ClassSubjects.SingleOrDefaultAsync(x => x.ClassSectionId == id && x.SubjectId == relatedId.Value, ct) ?? throw Missing();
            db.ClassSubjects.Remove(assignment);
            await db.SaveChangesAsync(ct);
            return;
        }
        var affected = resource switch
        {
            "years" when !await db.AcademicTerms.AnyAsync(x => x.AcademicYearId == id, ct) && !await db.ClassSections.AnyAsync(x => x.AcademicYearId == id, ct) => await db.AcademicYears.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "terms" => await db.AcademicTerms.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "education-stages" when !await db.ClassLevels.AnyAsync(x => x.EducationStageId == id, ct) => await db.EducationStages.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "class-levels" when !await db.ClassSections.AnyAsync(x => x.ClassLevelId == id, ct) => await db.ClassLevels.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "class-sections" when !await db.Enrollments.AnyAsync(x => x.ClassSectionId == id, ct) => await db.ClassSections.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "departments" when !await db.Subjects.AnyAsync(x => x.DepartmentId == id, ct) && !await db.StaffProfiles.AnyAsync(x => x.DepartmentId == id, ct) => await db.Departments.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
            "subjects" when !await db.ClassSubjects.AnyAsync(x => x.SubjectId == id, ct) && !await db.TeachingAssignments.AnyAsync(x => x.SubjectId == id, ct) => await db.Subjects.Where(x => x.Id == id).ExecuteDeleteAsync(ct),
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
    private static string ValidateAcademicYear(AcademicYearInput input)
    {
        var expectedName = $"{input.StartsOn.Year}/{input.EndsOn.Year}";
        if (input.EndsOn.Year != input.StartsOn.Year + 1 || !string.Equals(input.Name?.Trim(), expectedName, StringComparison.Ordinal))
            throw new ArgumentException("Select an academic session that matches the start and end date years.", nameof(input));
        return expectedName;
    }
    private static KeyNotFoundException Missing() => new("Academic structure item was not found.");

    private async Task<Guid> ResolveCurrentCampusAsync(CancellationToken ct)
    {
        if (tenant.CampusId is Guid campusId && await db.Campuses.AnyAsync(x => x.Id == campusId && x.IsActive, ct)) return campusId;
        var activeCampuses = await db.Campuses.Where(x => x.IsActive).Select(x => x.Id).Take(2).ToListAsync(ct);
        return activeCampuses.Count == 1 ? activeCampuses[0] : throw new InvalidOperationException("Select a campus workspace before creating a class section.");
    }

    private static async Task<string> UniqueCodeAsync(string name, Func<string, Task<bool>> existsAsync)
    {
        var normalized = string.Concat((name ?? string.Empty).Trim().ToUpperInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-'));
        while (normalized.Contains("--", StringComparison.Ordinal)) normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        var baseCode = normalized.Trim('-');
        if (baseCode.Length == 0) throw new ArgumentException("Enter a name containing letters or numbers.", nameof(name));
        if (baseCode.Length > 42) baseCode = baseCode[..42].TrimEnd('-');
        var candidate = baseCode;
        for (var suffix = 2; await existsAsync(candidate); suffix++) candidate = $"{baseCode}-{suffix}";
        return candidate;
    }

    private Task ManageAsync(Guid actor, CancellationToken ct) => DemandAsync(actor, Permissions.AcademicsManage, ct);
    private Task DemandAsync(Guid actor, string permission, CancellationToken ct) => access.DemandAsync(actor, permission, FeatureKeys.AcademicStructure, ct);
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
}

internal sealed record StandardEducationStage(string Name, string Code, int DisplayOrder)
{
    private static readonly IReadOnlyDictionary<string, StandardEducationStage> Stages = new[]
    {
        new StandardEducationStage("Creche / Early Years", "CRECHE", 10),
        new StandardEducationStage("Nursery", "NUR", 20),
        new StandardEducationStage("Primary", "PRI", 30),
        new StandardEducationStage("Junior Secondary", "JSS", 40),
        new StandardEducationStage("Senior Secondary", "SSS", 50),
        new StandardEducationStage("Sixth Form", "SIXTH", 60)
    }.ToDictionary(stage => stage.Name, StringComparer.OrdinalIgnoreCase);

    public static StandardEducationStage Resolve(string name)
    {
        var normalizedName = name?.Trim();
        if (normalizedName is not null && Stages.TryGetValue(normalizedName, out var stage)) return stage;
        throw new ArgumentException("Select a recognised education stage.", nameof(name));
    }
}

internal sealed record StandardClassLevel(string StageName, string Name, string Code, int DisplayOrder)
{
    private static readonly IReadOnlyDictionary<string, StandardClassLevel> Levels = new[]
    {
        new StandardClassLevel("Creche / Early Years", "Creche", "CRECHE", 10),
        new StandardClassLevel("Creche / Early Years", "Playgroup", "PLAYGROUP", 20),
        new StandardClassLevel("Creche / Early Years", "Reception", "RECEPTION", 30),
        new StandardClassLevel("Nursery", "Nursery 1", "NUR1", 10),
        new StandardClassLevel("Nursery", "Nursery 2", "NUR2", 20),
        new StandardClassLevel("Primary", "Primary 1", "PRI1", 10),
        new StandardClassLevel("Primary", "Primary 2", "PRI2", 20),
        new StandardClassLevel("Primary", "Primary 3", "PRI3", 30),
        new StandardClassLevel("Primary", "Primary 4", "PRI4", 40),
        new StandardClassLevel("Primary", "Primary 5", "PRI5", 50),
        new StandardClassLevel("Primary", "Primary 6", "PRI6", 60),
        new StandardClassLevel("Junior Secondary", "JSS 1", "JSS1", 10),
        new StandardClassLevel("Junior Secondary", "JSS 2", "JSS2", 20),
        new StandardClassLevel("Junior Secondary", "JSS 3", "JSS3", 30),
        new StandardClassLevel("Senior Secondary", "SS 1", "SS1", 10),
        new StandardClassLevel("Senior Secondary", "SS 2", "SS2", 20),
        new StandardClassLevel("Senior Secondary", "SS 3", "SS3", 30),
        new StandardClassLevel("Sixth Form", "Lower Sixth", "L6", 10),
        new StandardClassLevel("Sixth Form", "Upper Sixth", "U6", 20)
    }.ToDictionary(level => Key(level.StageName, level.Name), StringComparer.OrdinalIgnoreCase);

    public static StandardClassLevel Resolve(string stageName, string levelName)
    {
        var key = Key(stageName, levelName);
        if (Levels.TryGetValue(key, out var level)) return level;
        throw new ArgumentException("Select a class level that belongs to the chosen education stage.", nameof(levelName));
    }

    private static string Key(string stageName, string levelName) => $"{stageName?.Trim()}|{levelName?.Trim()}";
}

internal sealed record ClassSectionIdentity(string Name, string Code)
{
    public static ClassSectionIdentity Create(string levelName, string levelCode, string label)
    {
        var normalizedLabel = RemoveLevelPrefixes(levelName, label?.Trim() ?? string.Empty);
        if (normalizedLabel.Length is < 1 or > 50 || normalizedLabel.Any(character => !char.IsLetterOrDigit(character) && character is not ' ' and not '-'))
            throw new ArgumentException("Enter an arm or stream using letters, numbers, spaces or hyphens.", nameof(label));
        var codePart = string.Concat(normalizedLabel.ToUpperInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        var displayName = normalizedLabel.Length == 1 ? $"{levelName}{normalizedLabel.ToUpperInvariant()}" : $"{levelName} {normalizedLabel}";
        return new(displayName, $"{levelCode}-{codePart}");
    }

    private static string RemoveLevelPrefixes(string levelName, string label)
    {
        var result = label;
        while (TryConsumePrefix(levelName, result, out var remainder) && remainder.Length > 0) result = remainder;
        return result;
    }

    private static bool TryConsumePrefix(string levelName, string value, out string remainder)
    {
        var levelIndex = 0; var valueIndex = 0;
        while (levelIndex < levelName.Length)
        {
            while (levelIndex < levelName.Length && char.IsWhiteSpace(levelName[levelIndex])) levelIndex++;
            while (valueIndex < value.Length && char.IsWhiteSpace(value[valueIndex])) valueIndex++;
            if (levelIndex == levelName.Length) break;
            if (valueIndex == value.Length || char.ToUpperInvariant(levelName[levelIndex]) != char.ToUpperInvariant(value[valueIndex])) { remainder = value; return false; }
            levelIndex++; valueIndex++;
        }
        remainder = value[valueIndex..].Trim();
        return true;
    }
}
