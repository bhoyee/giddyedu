using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Tenancy.Domain;

public sealed class TenantMembership : ITenantOwned
{
    private TenantMembership() { }

    public TenantMembership(Guid id, Guid tenantId, Guid userId, DateTimeOffset createdAtUtc, string? roleAtSchool = null)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Membership identifiers are required.");
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
        RoleAtSchool = string.IsNullOrWhiteSpace(roleAtSchool) ? null : roleAtSchool.Trim();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsActive { get; private set; }
    public string? RoleAtSchool { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public void Suspend() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
