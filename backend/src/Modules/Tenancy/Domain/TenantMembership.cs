using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Tenancy.Domain;

public sealed class TenantMembership : ITenantOwned
{
    private TenantMembership() { }

    public TenantMembership(Guid id, Guid tenantId, Guid userId, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Membership identifiers are required.");
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
