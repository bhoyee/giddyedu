using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Identity.Domain;

public sealed class RefreshToken : ITenantOwned
{
    private RefreshToken() { }
    public RefreshToken(Guid id, Guid tenantId, Guid userId, Guid? campusId, string tokenHash, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    { Id = id; TenantId = tenantId; UserId = userId; CampusId = campusId; TokenHash = tokenHash; CreatedAtUtc = createdAtUtc; ExpiresAtUtc = expiresAtUtc; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? CampusId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public bool IsUsable(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;
    public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now;
}
