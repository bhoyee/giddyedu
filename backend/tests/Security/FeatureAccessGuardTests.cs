using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Subscriptions;

namespace GiddyEdu.SecurityTests;

public sealed class FeatureAccessGuardTests
{
    [Fact]
    public async Task DemandAsync_RejectsDisabledEntitlementDespitePermission()
    {
        var guard = new FeatureAccessGuard(new PermissionResult(true), new EntitlementResult(false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guard.DemandAsync(Guid.NewGuid(), "Students.View", "student-information"));
    }

    [Fact]
    public async Task DemandAsync_RejectsMissingPermissionDespiteEntitlement()
    {
        var guard = new FeatureAccessGuard(new PermissionResult(false), new EntitlementResult(true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guard.DemandAsync(Guid.NewGuid(), "Students.View", "student-information"));
    }

    private sealed class PermissionResult(bool allowed) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(allowed);
        public Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<string>>([]);
    }

    private sealed class EntitlementResult(bool enabled) : IEntitlementService
    {
        public Task<EffectiveEntitlement> GetAsync(string featureKey, Guid? campusId = null, CancellationToken cancellationToken = default) => Task.FromResult(new EffectiveEntitlement(enabled, null));
        public Task<bool> CanConsumeAsync(string featureKey, long quantity = 1, Guid? campusId = null, CancellationToken cancellationToken = default) => Task.FromResult(enabled);
        public Task RecordUsageAsync(string featureKey, long quantity = 1, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
