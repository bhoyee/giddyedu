using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Tenancy.Domain;

public sealed class Campus : ITenantOwned
{
    private Campus() { }

    public Campus(Guid id, Guid tenantId, string name, string code, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Campus and tenant identifiers are required.");
        Id = id;
        TenantId = tenantId;
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
}
