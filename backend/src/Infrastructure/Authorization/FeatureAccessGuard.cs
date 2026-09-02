using GiddyEdu.Infrastructure.Subscriptions;

namespace GiddyEdu.Infrastructure.Authorization;

public interface IFeatureAccessGuard
{
    Task DemandAsync(Guid actorUserId, string permission, string featureKey, CancellationToken ct = default);
}

public sealed class FeatureAccessGuard(IPermissionService permissions, IEntitlementService entitlements) : IFeatureAccessGuard
{
    public async Task DemandAsync(Guid actorUserId, string permission, string featureKey, CancellationToken ct = default)
    {
        if (!await permissions.HasPermissionAsync(actorUserId, permission, ct)) throw new UnauthorizedAccessException($"{permission} is required.");
        var entitlement = await entitlements.GetAsync(featureKey, null, ct);
        if (!entitlement.Enabled) throw new UnauthorizedAccessException($"The {featureKey} feature is not enabled for this tenant.");
    }
}
