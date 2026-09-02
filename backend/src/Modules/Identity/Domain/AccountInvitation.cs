using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Identity.Domain;

public enum InvitationTargetType { Staff, Guardian }

public sealed class AccountInvitation : ITenantOwned
{
    private AccountInvitation() { }

    public AccountInvitation(Guid id, Guid tenantId, InvitationTargetType targetType, Guid targetId, string email, string tokenHash, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || targetId == Guid.Empty) throw new ArgumentException("Invitation identifiers are required.");
        if (expiresAtUtc <= createdAtUtc) throw new ArgumentException("Invitation expiry must follow its creation time.", nameof(expiresAtUtc));
        Id = id; TenantId = tenantId; TargetType = targetType; TargetId = targetId;
        Email = string.IsNullOrWhiteSpace(email) || email.Trim().Length > 320 ? throw new ArgumentException("A valid invitation email is required.", nameof(email)) : email.Trim().ToUpperInvariant();
        TokenHash = string.IsNullOrWhiteSpace(tokenHash) ? throw new ArgumentException("Token hash is required.", nameof(tokenHash)) : tokenHash;
        CreatedAtUtc = createdAtUtc; ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public InvitationTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public string Email { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsUsable(DateTimeOffset now) => AcceptedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > now;
    public void Accept(DateTimeOffset now) { if (!IsUsable(now)) throw new InvalidOperationException("Invitation is no longer valid."); AcceptedAtUtc = now; }
    public void Revoke(DateTimeOffset now) { if (AcceptedAtUtc is not null) throw new InvalidOperationException("An accepted invitation cannot be revoked."); RevokedAtUtc ??= now; }
}
