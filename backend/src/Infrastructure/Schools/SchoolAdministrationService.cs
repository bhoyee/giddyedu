using System.Net.Mail;
using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Subscriptions;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.Schools.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Schools;

public sealed record SchoolProfileInput(string DisplayName, string SchoolSlug, string SchoolType, string? LegalName, string? Tagline, string? Email, string? Phone, string? WebsiteUrl, string? Address,
    string? State, string? LocalGovernment, string CountryCode, string TimeZone, string CurrencyCode, string DateFormat, string TimeFormat, string? CustomDomain,
    Guid? LogoFileId, Guid? PrincipalSignatureFileId, string? PrimaryColor, string? SecondaryColor);
public sealed record SchoolProfileInfo(string DisplayName, string SchoolSlug, string SchoolType, string? LegalName, string? Tagline, string? Email, string? Phone, string? WebsiteUrl, string? Address,
    string? State, string? LocalGovernment, string CountryCode, string TimeZone, string CurrencyCode, string DateFormat, string TimeFormat, string? CustomDomain,
    Guid? LogoFileId, Guid? PrincipalSignatureFileId, string? PrimaryColor, string? SecondaryColor, string? LogoUrl, string? PrincipalSignatureUrl);
public sealed record SchoolBrandingInfo(string DisplayName, string Scope, string? PrimaryColor, string? SecondaryColor, Guid? LogoFileId, Guid? PrincipalSignatureFileId, string? LogoUrl, string? PrincipalSignatureUrl);
public sealed record CampusInput(string Name, bool IsMainCampus);
public sealed record CampusInfo(Guid Id, string Name, string Code, bool IsMainCampus, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);

public interface ISchoolAdministrationService
{
    Task<SchoolProfileInfo?> GetProfileAsync(Guid actorUserId, CancellationToken cancellationToken = default);
    Task<SchoolBrandingInfo?> GetBrandingAsync(CancellationToken cancellationToken = default);
    Task UpsertBrandingAsync(Guid actorUserId, SchoolBrandingInfo input, CancellationToken cancellationToken = default);
    Task ClearCampusBrandingAsync(Guid actorUserId, CancellationToken cancellationToken = default);
    Task UpsertProfileAsync(Guid actorUserId, SchoolProfileInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CampusInfo>> ListCampusesAsync(Guid actorUserId, CancellationToken cancellationToken = default);
    Task<Guid> CreateCampusAsync(Guid actorUserId, CampusInput input, CancellationToken cancellationToken = default);
    Task UpdateCampusAsync(Guid actorUserId, Guid campusId, CampusInput input, CancellationToken cancellationToken = default);
    Task DeleteCampusAsync(Guid actorUserId, Guid campusId, CancellationToken cancellationToken = default);
}

public sealed class SchoolAdministrationService(GiddyEduDbContext db, ITenantContext tenant, IFeatureAccessGuard access, IFileService files, IClock clock) : ISchoolAdministrationService
{
    public async Task<SchoolBrandingInfo?> GetBrandingAsync(CancellationToken ct = default)
    {
        var profile = await db.SchoolProfiles.AsNoTracking().Select(x => new { x.DisplayName, x.PrimaryColor, x.SecondaryColor, x.LogoFileId, x.PrincipalSignatureFileId }).SingleOrDefaultAsync(ct);
        if (profile is null) return null;
        var effective = new SchoolBrandingInfo(profile.DisplayName, "Tenant", profile.PrimaryColor, profile.SecondaryColor, profile.LogoFileId, profile.PrincipalSignatureFileId, null, null);
        if (tenant.CampusId.HasValue)
        {
            var json = await db.CampusSettings.AsNoTracking().Where(x => x.CampusId == tenant.CampusId && x.Key == "school.branding").Select(x => x.ValueJson).SingleOrDefaultAsync(ct);
            if (json is not null && JsonSerializer.Deserialize<SchoolBrandingInfo>(json) is { } campus) effective = campus with { DisplayName = profile.DisplayName, Scope = "Campus" };
        }
        var logoUrl = effective.LogoFileId.HasValue ? await files.CreateDownloadUrlAsync(effective.LogoFileId.Value, ct) : null;
        var signatureUrl = effective.PrincipalSignatureFileId.HasValue ? await files.CreateDownloadUrlAsync(effective.PrincipalSignatureFileId.Value, ct) : null;
        return effective with { LogoUrl = logoUrl, PrincipalSignatureUrl = signatureUrl };
    }

    public async Task UpsertBrandingAsync(Guid actorUserId, SchoolBrandingInfo input, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct); var tenantId = RequireTenant();
        ValidateColor(input.PrimaryColor, nameof(input.PrimaryColor)); ValidateColor(input.SecondaryColor, nameof(input.SecondaryColor));
        var entityId = tenant.CampusId ?? tenantId;
        if (input.LogoFileId.HasValue && !await IsValidBrandAssetAsync(input.LogoFileId.Value, "branding-logo", tenantId, entityId, ct)) throw new ArgumentException("Invalid school logo.", nameof(input));
        if (input.PrincipalSignatureFileId.HasValue && !await IsValidBrandAssetAsync(input.PrincipalSignatureFileId.Value, "principal-signature", tenantId, entityId, ct)) throw new ArgumentException("Invalid principal signature.", nameof(input));
        if (!tenant.CampusId.HasValue)
        {
            var profile = await db.SchoolProfiles.SingleAsync(ct); profile.UpdateBranding(input.LogoFileId, input.PrincipalSignatureFileId, input.PrimaryColor, input.SecondaryColor, clock.UtcNow);
        }
        else
        {
            var value = JsonSerializer.Serialize(input with { DisplayName = string.Empty, Scope = "Campus", LogoUrl = null, PrincipalSignatureUrl = null });
            var setting = await db.CampusSettings.SingleOrDefaultAsync(x => x.CampusId == tenant.CampusId && x.Key == "school.branding", ct);
            if (setting is null) db.CampusSettings.Add(new CampusSetting(tenantId, tenant.CampusId.Value, "school.branding", value, clock.UtcNow)); else setting.Update(value, clock.UtcNow);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearCampusBrandingAsync(Guid actorUserId, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct);
        if (!tenant.CampusId.HasValue) throw new InvalidOperationException("A campus workspace is required.");
        var setting = await db.CampusSettings.SingleOrDefaultAsync(x => x.CampusId == tenant.CampusId && x.Key == "school.branding", ct);
        if (setting is not null) { db.CampusSettings.Remove(setting); await db.SaveChangesAsync(ct); }
    }

    public async Task<SchoolProfileInfo?> GetProfileAsync(Guid actorUserId, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsView, ct);
        var currentTenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == RequireTenant(), ct);
        if (currentTenant is null) return null;
        var profile = await db.SchoolProfiles.AsNoTracking().Select(x => new SchoolProfileInfo(x.DisplayName, currentTenant.Slug, x.SchoolType, x.LegalName, x.Tagline, x.Email, x.Phone, x.WebsiteUrl, x.Address,
            x.State, x.LocalGovernment, x.CountryCode, x.TimeZone, x.CurrencyCode, x.DateFormat, x.TimeFormat, x.CustomDomain, x.LogoFileId, x.PrincipalSignatureFileId, x.PrimaryColor, x.SecondaryColor, null, null)).SingleOrDefaultAsync(ct);
        if (profile is not null)
        {
            var logoUrl = profile.LogoFileId.HasValue ? await files.CreateDownloadUrlAsync(profile.LogoFileId.Value, ct) : null;
            var signatureUrl = profile.PrincipalSignatureFileId.HasValue ? await files.CreateDownloadUrlAsync(profile.PrincipalSignatureFileId.Value, ct) : null;
            return profile with { LogoUrl = logoUrl, PrincipalSignatureUrl = signatureUrl };
        }
        return new SchoolProfileInfo(currentTenant.Name, currentTenant.Slug, "Not specified", currentTenant.Name, null, null, null, null, null, null, null,
            "NG", "Africa/Lagos", "NGN", "dd/MM/yyyy", "HH:mm", null, null, null, "#12372A", "#10B981", null, null);
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
        if (input.LogoFileId.HasValue && !await IsValidBrandAssetAsync(input.LogoFileId.Value, "branding-logo", tenantId, tenantId, ct))
            throw new ArgumentException("LogoFileId must reference an available PNG or JPEG school logo owned by the current tenant.", nameof(input));
        if (input.PrincipalSignatureFileId.HasValue && !await IsValidBrandAssetAsync(input.PrincipalSignatureFileId.Value, "principal-signature", tenantId, tenantId, ct))
            throw new ArgumentException("PrincipalSignatureFileId must reference an available PNG or JPEG principal signature owned by the current tenant.", nameof(input));
        var profile = await db.SchoolProfiles.SingleOrDefaultAsync(ct);
        if (profile is null)
        {
            profile = new SchoolProfile(tenantId, input.DisplayName, input.SchoolType, input.LegalName ?? input.DisplayName, input.CountryCode, input.TimeZone, input.CurrencyCode, clock.UtcNow);
            db.SchoolProfiles.Add(profile);
        }
        profile.Update(input.DisplayName, input.SchoolType, input.LegalName, input.Tagline, input.Email, input.Phone, input.WebsiteUrl, input.Address,
            input.State, input.LocalGovernment, input.CountryCode, input.TimeZone, input.CurrencyCode, input.DateFormat, input.TimeFormat, customDomain,
            input.LogoFileId, input.PrincipalSignatureFileId, input.PrimaryColor, input.SecondaryColor, clock.UtcNow);
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
        var code = await GenerateCampusCodeAsync(input.Name, null, ct);
        db.Campuses.Add(new Campus(id, RequireTenant(), input.Name, code, clock.UtcNow, input.IsMainCampus)); await db.SaveChangesAsync(ct); return id;
    }

    public async Task UpdateCampusAsync(Guid actorUserId, Guid campusId, CampusInput input, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct);
        var campus = await db.Campuses.SingleOrDefaultAsync(x => x.Id == campusId, ct) ?? throw new KeyNotFoundException("Campus was not found.");
        if (input.IsMainCampus) await ClearMainCampusAsync(campusId, ct);
        campus.Update(input.Name, campus.Code, input.IsMainCampus, clock.UtcNow); await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCampusAsync(Guid actorUserId, Guid campusId, CancellationToken ct = default)
    {
        await DemandAsync(actorUserId, Permissions.SchoolsManage, ct);
        var campus = await db.Campuses.SingleOrDefaultAsync(x => x.Id == campusId, ct) ?? throw new KeyNotFoundException("Campus was not found.");
        if (await db.Campuses.CountAsync(ct) <= 1) throw new InvalidOperationException("A school must retain at least one campus.");
        if (tenant.CampusId == campusId) throw new InvalidOperationException("Switch to another campus workspace before deleting the current campus.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var sectionIds = await db.ClassSections.Where(x => x.CampusId == campusId).Select(x => x.Id).ToListAsync(ct);
        var enrollmentIds = await db.Enrollments.Where(x => sectionIds.Contains(x.ClassSectionId)).Select(x => x.Id).ToListAsync(ct);
        var staffIds = await db.StaffProfiles.Where(x => x.CampusId == campusId).Select(x => x.Id).ToListAsync(ct);
        var ownedEntityIds = sectionIds.Concat(enrollmentIds).Concat(staffIds).Append(campusId).ToArray();
        var storedFileIds = await db.StoredFiles.Where(x => ownedEntityIds.Contains(x.EntityId)).Select(x => x.Id).ToListAsync(ct);
        foreach (var fileId in storedFileIds) await files.DeleteAsync(fileId, ct);
        await db.StudentProgressions.Where(x => enrollmentIds.Contains(x.FromEnrollmentId) || enrollmentIds.Contains(x.ToEnrollmentId)).ExecuteDeleteAsync(ct);
        await db.Enrollments.Where(x => enrollmentIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.TeachingAssignments.Where(x => sectionIds.Contains(x.ClassSectionId)).ExecuteDeleteAsync(ct);
        await db.ClassSections.Where(x => x.CampusId == campusId).ExecuteDeleteAsync(ct);
        await db.StaffProfiles.Where(x => x.CampusId == campusId).ExecuteDeleteAsync(ct);
        await db.CampusSettings.Where(x => x.CampusId == campusId).ExecuteDeleteAsync(ct);
        await db.CampusEntitlementOverrides.Where(x => x.CampusId == campusId).ExecuteDeleteAsync(ct);
        await db.RefreshTokens.Where(x => x.CampusId == campusId).ExecuteDeleteAsync(ct);
        db.Campuses.Remove(campus);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
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
    private async Task<string> GenerateCampusCodeAsync(string name, Guid? exceptCampusId, CancellationToken ct)
    {
        var baseCode = new string(string.Join('-', name.Trim().ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Select(character => char.IsAsciiLetterOrDigit(character) || character == '-' ? character : '-').ToArray());
        while (baseCode.Contains("--", StringComparison.Ordinal)) baseCode = baseCode.Replace("--", "-", StringComparison.Ordinal);
        baseCode = baseCode.Trim('-');
        if (string.IsNullOrWhiteSpace(baseCode)) throw new ArgumentException("Campus name must contain letters or numbers.", nameof(name));
        baseCode = baseCode[..Math.Min(baseCode.Length, 40)].TrimEnd('-');
        var candidate = baseCode;
        for (var suffix = 2; await db.Campuses.AnyAsync(x => x.Code == candidate && (!exceptCampusId.HasValue || x.Id != exceptCampusId), ct); suffix++)
            candidate = $"{baseCode[..Math.Min(baseCode.Length, 40 - suffix.ToString().Length - 1)]}-{suffix}";
        return candidate;
    }
    private Task<bool> IsValidBrandAssetAsync(Guid fileId, string category, Guid tenantId, Guid entityId, CancellationToken ct) => db.StoredFiles.AnyAsync(x =>
        x.Id == fileId && x.TenantId == tenantId && x.Status == StoredFileStatus.Available && x.Category == category && x.EntityType == "SchoolProfile" &&
        (x.EntityId == entityId || x.EntityId == tenantId) && x.SizeBytes <= 2 * 1024 * 1024 && (x.ContentType == "image/png" || x.ContentType == "image/jpeg"), ct);
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
