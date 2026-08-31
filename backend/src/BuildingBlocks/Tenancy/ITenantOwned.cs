namespace GiddyEdu.BuildingBlocks.Tenancy;

public interface ITenantOwned
{
    Guid TenantId { get; }
}
