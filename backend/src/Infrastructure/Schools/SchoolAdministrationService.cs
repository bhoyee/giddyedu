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

public sealed record SchoolProfileInput(string DisplayName, string SchoolSlug, string SchoolType, string? LegalName, string? Tagline, string? Email, string? Phone, string? WebsiteUrl, string? Address,
    string? State, string? LocalGovernment, string CountryCode, string TimeZone, string CurrencyCode, string DateFormat, string TimeFormat, string? CustomDomain,
    Guid? LogoFileId, string? PrimaryColor, string? SecondaryColor);
public sealed record SchoolProfileInfo(string DisplayName, string SchoolSlug, string SchoolType, string? LegalName, string? Tagline, string? Email, string? Phone, string? WebsiteUrl, string? Address,
    string? State, string? LocalGovernment, string CountryCode, string TimeZone, string CurrencyCode, string DateFormat, string TimeFormat, string? CustomDomain,
    Guid? LogoFileId, string? PrimaryColor, string? SecondaryColor);
public sealed record CampusInput(string Name, string Code, bool IsMainCampus);
public sealed record CampusInfo(Guid Id, string Name, string Code, bool IsMainCampus, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);

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
        var currentTenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == RequireTenant(), ct);
        if (currentTenant is null) return null;
        var profile = await db.SchoolProfiles.AsNoTracking().Select(x => new SchoolProfileInfo(x.DisplayName, currentTenant.Slug, x.SchoolType, x.LegalName, x.Tagline, x.Email, x.Phone, x.WebsiteUrl, x.Address,
            x.State, x.LocalGovernment, x.CountryCode, x.TimeZone, x.CurrencyCode, x.DateFormat, x.TimeFormat, x.CustomDomain, x.LogoFileId, x.PrimaryColor, x.SecondaryColor)).SingleOrDefaultAsync(ct);
        if (profile is not null) return profile;
        return new SchoolProfileInfo(currentTenant.Name, currentTenant.Slug, "Not specified", currentTenant.Name, null, null, null, null, null, null, null,
            "NG", "Africa/Lagos", "NGN", "dd/MM/yyyy", "HH:mm", null, null, "#12372A", "#10B981");
    }

    public async Task UpsertProfileAsync(Guid actorUserId, SchoolProfileInput input, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct); Validate(input); var tenantId = RequireTenant();
        var schoolSlug = input.SchoolSlug.Trim().ToLowerInvariant();
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id != tenantId && x.Slug == schoolSlug, ct)) throw new ArgumentException("School URL is already in use.", nameof(input));
        var customDomain = NormalizeDomain(input.CustomDomain);
        if (customDomain is not null && await db.SchoolProfiles.IgnoreQueryFilters().AnyAsync(x => x.TenantId != tenantId && x.CustomDomain == customDomain, ct))
            throw new ArgumentException("Custom domain is already in use.", nameof(input));
        var currentTenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(x => x.Id == tenantId, ct);
        currentTenant.UpdateIdentity(input.DisplayName, schoolSlug, clock.UtcNow);
        if (input.LogoFileId.HasValue && !await db.StoredFiles.AnyAsync(x => x.Id == input.LogoFileId && x.Status == StoredFileStatus.Available, ct))
            throw new ArgumentException("LogoFileId must reference an available file owned by the current tenant.", nameof(input));
        var profile = await db.SchoolProfiles.SingleOrDefaultAsync(ct);
        if (profile is null)
        {
            profile = new SchoolProfile(tenantId, input.DisplayName, input.SchoolType, input.LegalName ?? input.DisplayName, input.CountryCode, input.TimeZone, input.CurrencyCode, clock.UtcNow);
            db.SchoolProfiles.Add(profile);
        }
        profile.Update(input.DisplayName, input.SchoolType, input.LegalName, input.Tagline, input.Email, input.Phone, input.WebsiteUrl, input.Address,
            input.State, input.LocalGovernment, input.CountryCode, input.TimeZone, input.CurrencyCode, input.DateFormat, input.TimeFormat, customDomain,
            input.LogoFileId, input.PrimaryColor, input.SecondaryColor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<CampusInfo>> ListCampusesAsync(Guid actorUserId, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsView, ct);
        return await db.Campuses.AsNoTracking().OrderByDescending(x => x.IsMainCampus).ThenBy(x => x.Name).Select(x => new CampusInfo(x.Id, x.Name, x.Code, x.IsMainCampus, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc)).ToListAsync(ct);
    }

    public async Task<Guid> CreateCampusAsync(Guid actorUserId, CampusInput input, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct); var id = Guid.NewGuid();
        if (input.IsMainCampus) await ClearMainCampusAsync(null, ct);
        db.Campuses.Add(new Campus(id, RequireTenant(), input.Name, input.Code, clock.UtcNow, input.IsMainCampus)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task UpdateCampusAsync(Guid actorUserId, Guid campusId, CampusInput input, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct);
        var campus = await db.Campuses.SingleOrDefaultAsync(x => x.Id == campusId, ct) ?? throw new KeyNotFoundException("Campus was not found.");
        if (input.IsMainCampus) await ClearMainCampusAsync(campusId, ct);
        campus.Update(input.Name, input.Code, input.IsMainCampus, clock.UtcNow); await db.SaveChangesAsync(ct);
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
        var schoolTypes = new[] { "Nursery", "Primary", "Secondary", "Primary and Secondary", "Nursery, Primary and Secondary" };
        if (!schoolTypes.Contains(input.SchoolType, StringComparer.Ordinal)) throw new ArgumentException("SchoolType is not supported.", nameof(input));
        var schoolSlug = input.SchoolSlug.Trim().ToLowerInvariant();
        if (schoolSlug.Length is < 3 or > 100 || schoolSlug[0] == '-' || schoolSlug[^1] == '-' || schoolSlug.Contains("--", StringComparison.Ordinal) || schoolSlug.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("SchoolSlug must contain only letters, numbers and single hyphens between words.", nameof(input));
        if (input.Email is not null) { try { _ = new MailAddress(input.Email); } catch (FormatException) { throw new ArgumentException("Email is invalid.", nameof(input)); } }
        if (input.WebsiteUrl is not null && (!Uri.TryCreate(input.WebsiteUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))) throw new ArgumentException("WebsiteUrl must be an HTTP or HTTPS URL.", nameof(input));
        if (input.CountryCode.Length != 2 || !input.CountryCode.All(char.IsLetter)) throw new ArgumentException("CountryCode must be a two-letter code.", nameof(input));
        if (input.CurrencyCode.Length != 3 || !input.CurrencyCode.All(char.IsLetter)) throw new ArgumentException("CurrencyCode must be a three-letter code.", nameof(input));
        if (input.CountryCode.Equals("NG", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(input.State) || string.IsNullOrWhiteSpace(input.LocalGovernment)))
            throw new ArgumentException("State and LocalGovernment are required for Nigerian schools.", nameof(input));
        if (input.DateFormat is not ("dd/MM/yyyy" or "MM/dd/yyyy" or "yyyy-MM-dd")) throw new ArgumentException("DateFormat is not supported.", nameof(input));
        if (input.TimeFormat is not ("HH:mm" or "hh:mm tt")) throw new ArgumentException("TimeFormat is not supported.", nameof(input));
        _ = NormalizeDomain(input.CustomDomain);
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZone); } catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new ArgumentException("TimeZone is not recognized.", nameof(input)); }
        if (input.Phone is not null && !IsPhone(input.Phone)) throw new ArgumentException("Phone must contain exactly 11 digits.", nameof(input));
        ValidateColor(input.PrimaryColor, nameof(input.PrimaryColor)); ValidateColor(input.SecondaryColor, nameof(input.SecondaryColor));
    }

    private async Task ClearMainCampusAsync(Guid? exceptCampusId, CancellationToken ct)
    {
        var existing = await db.Campuses.Where(x => x.IsMainCampus && (!exceptCampusId.HasValue || x.Id != exceptCampusId.Value)).ToListAsync(ct);
        foreach (var campus in existing) campus.SetMainCampus(false, clock.UtcNow);
    }
    private static void ValidateColor(string? color, string name) { if (color is not null && (color.Length != 7 || color[0] != '#' || !color[1..].All(Uri.IsHexDigit))) throw new ArgumentException($"{name} must use #RRGGBB format.", name); }
    private static bool IsPhone(string value) => value.Length == 11 && value.All(char.IsAsciiDigit);
    private static string? NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var domain = value.Trim().TrimEnd('.').ToLowerInvariant();
        if (domain.Length > 253 || domain.Contains('/') || domain.Contains(':') || Uri.CheckHostName(domain) is UriHostNameType.Unknown)
            throw new ArgumentException("CustomDomain must be a valid host name without http://, https:// or a path.", nameof(value));
        return domain;
    }
    private Task DemandAsync(Guid actor, string permission, CancellationToken ct) => access.DemandAsync(actor, permission, FeatureKeys.SchoolAdministration, ct);
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
}
