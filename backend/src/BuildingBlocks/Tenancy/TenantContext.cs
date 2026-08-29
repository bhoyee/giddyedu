namespace GiddyEdu.BuildingBlocks.Tenancy;

public sealed record TenantContext(Guid? TenantId, Guid? CampusId) : ITenantContext
{
    public bool HasTenant => TenantId.HasValue;
}
