namespace GiddyEdu.BuildingBlocks.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? CampusId { get; }
    bool HasTenant { get; }
}
