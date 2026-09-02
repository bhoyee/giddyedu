using System.Net.Mail;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Subscriptions;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.Schools.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Schools;

public sealed record SchoolProfileInput(string DisplayName, string? LegalName, string? Email, string? Phone, string? WebsiteUrl, string? Address,
    string CountryCode, string TimeZone, string CurrencyCode, Guid? LogoFileId, string? PrimaryColor, string? SecondaryColor);
public sealed record SchoolProfileInfo(string DisplayName, string? LegalName, string? Email, string? Phone, string? WebsiteUrl, string? Address,
    string CountryCode, string TimeZone, string CurrencyCode, Guid? LogoFileId, string? PrimaryColor, string? SecondaryColor);
public sealed record CampusInput(string Name, string Code);
public sealed record CampusInfo(Guid Id, string Name, string Code, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);

public interface ISchoolAdministrationService
{
    Task<SchoolProfileInfo?> GetProfileAsync(Guid actorUserId, CancellationToken cancellationToken = default);
    Task UpsertProfileAsync(Guid actorUserId, SchoolProfileInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CampusInfo>> ListCampusesAsync(Guid actorUserId, CancellationToken cancellationToken = default);
    Task<Guid> CreateCampusAsync(Guid actorUserId, CampusInput input, CancellationToken cancellationToken = default);
    Task UpdateCampusAsync(Guid actorUserId, Guid campusId, CampusInput input, CancellationToken cancellationToken = default);
    Task DeactivateCampusAsync(Guid actorUserId, Guid campusId, CancellationToken cancellationToken = default);
}

public sealed class SchoolAdministrationService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IClock clock) : ISchoolAdministrationService
{
    public async Task<SchoolProfileInfo?> GetProfileAsync(Guid actorUserId, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsView, ct);
        return await db.SchoolProfiles.AsNoTracking().Select(x => new SchoolProfileInfo(x.DisplayName, x.LegalName, x.Email, x.Phone, x.WebsiteUrl, x.Address,
            x.CountryCode, x.TimeZone, x.CurrencyCode, x.LogoFileId, x.PrimaryColor, x.SecondaryColor)).SingleOrDefaultAsync(ct);
    }

    public async Task UpsertProfileAsync(Guid actorUserId, SchoolProfileInput input, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct); Validate(input); var tenantId = RequireTenant();
        if (input.LogoFileId.HasValue && !await db.StoredFiles.AnyAsync(x => x.Id == input.LogoFileId && x.Status == StoredFileStatus.Available, ct))
            throw new ArgumentException("LogoFileId must reference an available file owned by the current tenant.", nameof(input));
        var profile = await db.SchoolProfiles.SingleOrDefaultAsync(ct);
        if (profile is null)
        {
            profile = new SchoolProfile(tenantId, input.DisplayName, input.CountryCode, input.TimeZone, input.CurrencyCode, clock.UtcNow);
            db.SchoolProfiles.Add(profile);
        }
        profile.Update(input.DisplayName, input.LegalName, input.Email, input.Phone, input.WebsiteUrl, input.Address, input.CountryCode, input.TimeZone,
            input.CurrencyCode, input.LogoFileId, input.PrimaryColor, input.SecondaryColor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<CampusInfo>> ListCampusesAsync(Guid actorUserId, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsView, ct);
        return await db.Campuses.AsNoTracking().OrderBy(x => x.Name).Select(x => new CampusInfo(x.Id, x.Name, x.Code, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc)).ToListAsync(ct);
    }

    public async Task<Guid> CreateCampusAsync(Guid actorUserId, CampusInput input, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct); var id = Guid.NewGuid();
        db.Campuses.Add(new Campus(id, RequireTenant(), input.Name, input.Code, clock.UtcNow)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task UpdateCampusAsync(Guid actorUserId, Guid campusId, CampusInput input, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct);
        var campus = await db.Campuses.SingleOrDefaultAsync(x => x.Id == campusId, ct) ?? throw new KeyNotFoundException("Campus was not found.");
        campus.Update(input.Name, input.Code, clock.UtcNow); await db.SaveChangesAsync(ct);
    }

    public async Task DeactivateCampusAsync(Guid actorUserId, Guid campusId, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct);
        var campus = await db.Campuses.SingleOrDefaultAsync(x => x.Id == campusId, ct) ?? throw new KeyNotFoundException("Campus was not found.");
        if (await db.Campuses.CountAsync(x => x.IsActive, ct) <= 1) throw new InvalidOperationException("A school must retain at least one active campus.");
        campus.Deactivate(clock.UtcNow); await db.SaveChangesAsync(ct);
    }

    private static void Validate(SchoolProfileInput input)
    {
        if (input.Email is not null) { try { _ = new MailAddress(input.Email); } catch (FormatException) { throw new ArgumentException("Email is invalid.", nameof(input)); } }
        if (input.WebsiteUrl is not null && (!Uri.TryCreate(input.WebsiteUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))) throw new ArgumentException("WebsiteUrl must be an HTTP or HTTPS URL.", nameof(input));
        if (input.CountryCode.Length != 2 || !input.CountryCode.All(char.IsLetter)) throw new ArgumentException("CountryCode must be a two-letter code.", nameof(input));
        if (input.CurrencyCode.Length != 3 || !input.CurrencyCode.All(char.IsLetter)) throw new ArgumentException("CurrencyCode must be a three-letter code.", nameof(input));
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZone); } catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new ArgumentException("TimeZone is not recognized.", nameof(input)); }
        if (input.Phone is not null && !IsPhone(input.Phone)) throw new ArgumentException("Phone contains an invalid phone number.", nameof(input));
        ValidateColor(input.PrimaryColor, nameof(input.PrimaryColor)); ValidateColor(input.SecondaryColor, nameof(input.SecondaryColor));
    }
    private static void ValidateColor(string? color, string name) { if (color is not null && (color.Length != 7 || color[0] != '#' || !color[1..].All(Uri.IsHexDigit))) throw new ArgumentException($"{name} must use #RRGGBB format.", name); }
    private static bool IsPhone(string value) { var digits = value.Count(char.IsDigit); return digits is >= 7 and <= 15 && value.All(c => char.IsDigit(c) || c is '+' or ' ' or '-' or '(' or ')'); }
    private Task DemandAsync(Guid actor, string permission, CancellationToken ct) => access.DemandAsync(actor, permission, FeatureKeys.SchoolAdministration, ct);
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
}
