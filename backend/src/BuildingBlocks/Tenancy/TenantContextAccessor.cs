namespace GiddyEdu.BuildingBlocks.Tenancy;

public sealed class TenantContextAccessor : ITenantContext, ITenantContextSetter
{
    public Guid? TenantId { get; private set; }
    public Guid? CampusId { get; private set; }
    public bool HasTenant => TenantId.HasValue;

    public void Set(Guid tenantId, Guid? campusId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identifier cannot be empty.", nameof(tenantId));
        TenantId = tenantId;
        CampusId = campusId;
    }

    public void Clear()
    {
        TenantId = null;
        CampusId = null;
    }
}
