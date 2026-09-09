using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Subscriptions.Domain;

public enum EntitlementValueType { Boolean, Limit }
public enum SubscriptionStatus { Active, GracePeriod, Suspended, Expired, Cancelled }

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
    public TenantSubscription(Guid id, Guid tenantId, Guid planId, DateTimeOffset startsAtUtc, DateTimeOffset? endsAtUtc = null)
    { if (id == Guid.Empty || tenantId == Guid.Empty || planId == Guid.Empty) throw new ArgumentException("Subscription identifiers are required."); if (endsAtUtc.HasValue && endsAtUtc <= startsAtUtc) throw new ArgumentOutOfRangeException(nameof(endsAtUtc)); Id = id; TenantId = tenantId; PlanId = planId; StartsAtUtc = startsAtUtc; EndsAtUtc = endsAtUtc; Status = SubscriptionStatus.Active; IsActive = true; UpdatedAtUtc = startsAtUtc; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PlanId { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset? EndsAtUtc { get; private set; }
    public DateTimeOffset? GraceEndsAtUtc { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void Renew(DateTimeOffset endsAtUtc, DateTimeOffset now) { if (Status == SubscriptionStatus.Cancelled) throw new InvalidOperationException("A cancelled subscription cannot be renewed."); if (endsAtUtc <= now || endsAtUtc <= StartsAtUtc) throw new ArgumentOutOfRangeException(nameof(endsAtUtc)); EndsAtUtc = endsAtUtc; GraceEndsAtUtc = null; Status = SubscriptionStatus.Active; IsActive = true; UpdatedAtUtc = now; }
    public void BeginGracePeriod(DateTimeOffset graceEndsAtUtc, DateTimeOffset now) { if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Suspended) throw new InvalidOperationException("This subscription cannot enter a grace period."); if (graceEndsAtUtc <= now) throw new ArgumentOutOfRangeException(nameof(graceEndsAtUtc)); GraceEndsAtUtc = graceEndsAtUtc; Status = SubscriptionStatus.GracePeriod; IsActive = true; UpdatedAtUtc = now; }
    public void Suspend(DateTimeOffset now) { if (Status == SubscriptionStatus.Cancelled) throw new InvalidOperationException("A cancelled subscription cannot be suspended."); Status = SubscriptionStatus.Suspended; IsActive = false; UpdatedAtUtc = now; }
    public void Reactivate(DateTimeOffset endsAtUtc, DateTimeOffset now) { if (Status != SubscriptionStatus.Suspended && Status != SubscriptionStatus.Expired) throw new InvalidOperationException("Only suspended or expired subscriptions can be reactivated."); Renew(endsAtUtc, now); }
    public void Expire(DateTimeOffset now) { if (Status == SubscriptionStatus.Cancelled) return; EndsAtUtc ??= now; GraceEndsAtUtc = null; Status = SubscriptionStatus.Expired; IsActive = false; UpdatedAtUtc = now; }
    public void End(DateTimeOffset endsAtUtc) { if (endsAtUtc < StartsAtUtc) throw new ArgumentOutOfRangeException(nameof(endsAtUtc)); EndsAtUtc = endsAtUtc; GraceEndsAtUtc = null; Status = SubscriptionStatus.Cancelled; IsActive = false; UpdatedAtUtc = endsAtUtc; }
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
