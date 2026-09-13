using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Tenancy.Domain;

public sealed class Campus : ITenantOwned
{
    private Campus() { }

    public Campus(Guid id, Guid tenantId, string name, string code, DateTimeOffset createdAtUtc, bool isMainCampus = false)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Campus and tenant identifiers are required.");
        Id = id;
        TenantId = tenantId;
        SetNameAndCode(name, code);
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
        IsMainCampus = isMainCampus;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public bool IsMainCampus { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public void Update(string name, string code, bool isMainCampus, DateTimeOffset updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new ArgumentException("Campus name is required and must not exceed 200 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50) throw new ArgumentException("Campus code is required and must not exceed 50 characters.", nameof(code));
        SetNameAndCode(name, code); IsMainCampus = isMainCampus; UpdatedAtUtc = updatedAtUtc;
    }

    public void SetMainCampus(bool isMainCampus, DateTimeOffset updatedAtUtc) { IsMainCampus = isMainCampus; UpdatedAtUtc = updatedAtUtc; }

    public void Deactivate(DateTimeOffset updatedAtUtc) { IsActive = false; UpdatedAtUtc = updatedAtUtc; }

    private void SetNameAndCode(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new ArgumentException("Campus name is required and must not exceed 200 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50) throw new ArgumentException("Campus code is required and must not exceed 50 characters.", nameof(code));
        Name = name.Trim(); Code = code.Trim().ToUpperInvariant();
    }
}
