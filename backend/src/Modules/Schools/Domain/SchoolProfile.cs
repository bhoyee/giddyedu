using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Schools.Domain;

public sealed class SchoolProfile : ITenantOwned
{
    private SchoolProfile() { }

    public SchoolProfile(Guid tenantId, string displayName, string schoolType, string? legalName, string countryCode, string timeZone, string currencyCode, DateTimeOffset createdAtUtc)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant identifier is required.", nameof(tenantId));
        TenantId = tenantId;
        CreatedAtUtc = createdAtUtc;
        Update(displayName, schoolType, legalName, null, null, null, null, null, null, null, countryCode, timeZone, currencyCode, "dd/MM/yyyy", "HH:mm", null, null, null, null, createdAtUtc);
        UpdatedAtUtc = null;
    }

    public Guid TenantId { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public string SchoolType { get; private set; } = null!;
    public string? LegalName { get; private set; }
    public string? Tagline { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public string? Address { get; private set; }
    public string? State { get; private set; }
    public string? LocalGovernment { get; private set; }
    public string CountryCode { get; private set; } = null!;
    public string TimeZone { get; private set; } = null!;
    public string CurrencyCode { get; private set; } = null!;
    public string DateFormat { get; private set; } = null!;
    public string TimeFormat { get; private set; } = null!;
    public string? CustomDomain { get; private set; }
    public Guid? LogoFileId { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public void Update(string displayName, string schoolType, string? legalName, string? tagline, string? email, string? phone, string? websiteUrl, string? address,
        string? state, string? localGovernment, string countryCode, string timeZone, string currencyCode, string dateFormat, string timeFormat, string? customDomain,
        Guid? logoFileId, string? primaryColor, string? secondaryColor, DateTimeOffset updatedAtUtc)
    {
        DisplayName = Required(displayName, nameof(displayName), 200);
        SchoolType = Required(schoolType, nameof(schoolType), 50);
        LegalName = Optional(legalName, 250); Tagline = Optional(tagline, 160); Email = Optional(email, 320); Phone = Optional(phone, 30);
        WebsiteUrl = Optional(websiteUrl, 500); Address = Optional(address, 1000);
        State = Optional(state, 100); LocalGovernment = Optional(localGovernment, 150);
        CountryCode = Required(countryCode, nameof(countryCode), 2).ToUpperInvariant();
        TimeZone = Required(timeZone, nameof(timeZone), 100);
        CurrencyCode = Required(currencyCode, nameof(currencyCode), 3).ToUpperInvariant();
        DateFormat = Required(dateFormat, nameof(dateFormat), 20);
        TimeFormat = Required(timeFormat, nameof(timeFormat), 20);
        CustomDomain = Optional(customDomain, 253)?.ToLowerInvariant();
        LogoFileId = logoFileId; PrimaryColor = Optional(primaryColor, 7); SecondaryColor = Optional(secondaryColor, 7); UpdatedAtUtc = updatedAtUtc;
    }

    private static string Required(string value, string name, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max
        ? throw new ArgumentException($"{name} is required and must not exceed {max} characters.", name) : value.Trim();
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null
        : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}
