namespace GiddyEdu.BuildingBlocks.Tenancy;

public interface ITenantContextSetter
{
    void Set(Guid tenantId, Guid? campusId);
    void Clear();
}
