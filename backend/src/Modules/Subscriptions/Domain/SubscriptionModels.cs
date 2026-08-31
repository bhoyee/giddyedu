using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Subscriptions.Domain;

public enum EntitlementValueType { Boolean, Limit }

public sealed class Plan
{
    private Plan() { }
    public Plan(Guid id, string code, string name) { Id = id; Code = code.Trim().ToLowerInvariant(); Name = name.Trim(); }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
}

public sealed class Feature
{
    private Feature() { }
    public Feature(Guid id, string key, string name, EntitlementValueType valueType) { Id = id; Key = key.Trim(); Name = name.Trim(); ValueType = valueType; }
    public Guid Id { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public EntitlementValueType ValueType { get; private set; }
}

public sealed class PlanEntitlement
{
    private PlanEntitlement() { }
    public PlanEntitlement(Guid planId, Guid featureId, bool? enabled, long? limit) { PlanId = planId; FeatureId = featureId; Enabled = enabled; Limit = limit; }
    public Guid PlanId { get; private set; }
    public Guid FeatureId { get; private set; }
    public bool? Enabled { get; private set; }
    public long? Limit { get; private set; }
}

public sealed class TenantSubscription : ITenantOwned
{
    private TenantSubscription() { }
    public TenantSubscription(Guid id, Guid tenantId, Guid planId, DateTimeOffset startsAtUtc)
    { Id = id; TenantId = tenantId; PlanId = planId; StartsAtUtc = startsAtUtc; IsActive = true; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PlanId { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset? EndsAtUtc { get; private set; }
    public bool IsActive { get; private set; }
}

public sealed class TenantEntitlementOverride : ITenantOwned
{
    private TenantEntitlementOverride() { }
    public TenantEntitlementOverride(Guid tenantId, Guid featureId, bool? enabled, long? limit)
    { TenantId = tenantId; FeatureId = featureId; Enabled = enabled; Limit = limit; }
    public Guid TenantId { get; private set; }
    public Guid FeatureId { get; private set; }
    public bool? Enabled { get; private set; }
    public long? Limit { get; private set; }
}

public sealed class CampusEntitlementOverride : ITenantOwned
{
    private CampusEntitlementOverride() { }
    public CampusEntitlementOverride(Guid tenantId, Guid campusId, Guid featureId, bool? enabled, long? limit)
    { TenantId = tenantId; CampusId = campusId; FeatureId = featureId; Enabled = enabled; Limit = limit; }
    public Guid TenantId { get; private set; }
    public Guid CampusId { get; private set; }
    public Guid FeatureId { get; private set; }
    public bool? Enabled { get; private set; }
    public long? Limit { get; private set; }
}

public sealed class AddOn
{
    private AddOn() { }
    public AddOn(Guid id, string code, string name) { Id = id; Code = code.Trim(); Name = name.Trim(); }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
}

public sealed class TenantAddOn : ITenantOwned
{
    private TenantAddOn() { }
    public TenantAddOn(Guid tenantId, Guid addOnId) { TenantId = tenantId; AddOnId = addOnId; }
    public Guid TenantId { get; private set; }
    public Guid AddOnId { get; private set; }
}

public sealed class FeatureUsage : ITenantOwned
{
    private FeatureUsage() { }
    public FeatureUsage(Guid id, Guid tenantId, Guid featureId, DateOnly periodStart, long quantity)
    { Id = id; TenantId = tenantId; FeatureId = featureId; PeriodStart = periodStart; Quantity = quantity; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FeatureId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public long Quantity { get; private set; }
    public void Add(long quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        checked { Quantity += quantity; }
    }
}
