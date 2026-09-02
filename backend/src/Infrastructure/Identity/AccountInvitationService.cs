using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Messaging;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GiddyEdu.Infrastructure.Identity;

public sealed record CreateAccountInvitationInput(InvitationTargetType TargetType, Guid TargetId);
public sealed record AcceptAccountInvitationInput(string Token, string DisplayName, string Password);
public sealed record AccountInvitationInfo(Guid Id, InvitationTargetType TargetType, Guid TargetId, string Email, DateTimeOffset ExpiresAtUtc);

public interface IAccountInvitationService
{
    Task<AccountInvitationInfo> CreateAsync(Guid actorUserId, CreateAccountInvitationInput input, CancellationToken cancellationToken = default);
    Task<Guid> AcceptAsync(AcceptAccountInvitationInput input, CancellationToken cancellationToken = default);
}

public sealed class AccountInvitationService(GiddyEduDbContext db, ITenantContext tenant, ITenantContextSetter tenantSetter,
    IPermissionService permissions, UserManager<PlatformUser> users, INotificationQueue notifications, IConfiguration configuration, IClock clock) : IAccountInvitationService
{
    public async Task<AccountInvitationInfo> CreateAsync(Guid actorUserId, CreateAccountInvitationInput input, CancellationToken ct = default)
    {
        if (!await permissions.HasPermissionAsync(actorUserId, Permissions.UsersManage, ct)) throw new UnauthorizedAccessException("Users.Manage permission is required.");
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        var email = input.TargetType switch
        {
            InvitationTargetType.Staff => await db.StaffProfiles.Where(x => x.Id == input.TargetId && x.UserId == null).Select(x => x.WorkEmail).SingleOrDefaultAsync(ct),
            InvitationTargetType.Guardian => await db.Guardians.Where(x => x.Id == input.TargetId && x.UserId == null).Select(x => x.Email).SingleOrDefaultAsync(ct),
            _ => null
        };
        if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("The unlinked record must have an email address before it can be invited.");

        var now = clock.UtcNow;
        foreach (var previous in await db.AccountInvitations.Where(x => x.TargetType == input.TargetType && x.TargetId == input.TargetId && x.AcceptedAtUtc == null && x.RevokedAtUtc == null).ToListAsync(ct)) previous.Revoke(now);
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        var invitation = new AccountInvitation(Guid.NewGuid(), tenantId, input.TargetType, input.TargetId, email, Hash(rawToken), now, now.AddDays(3));
        db.AccountInvitations.Add(invitation); await db.SaveChangesAsync(ct);

        var baseUrl = configuration["App:PublicBaseUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
        var link = $"{baseUrl}/accept-invitation?token={Uri.EscapeDataString(rawToken)}";
        var payload = JsonSerializer.Serialize(new EmailNotificationPayload("Your GiddyEdu invitation", $"<p>You have been invited to GiddyEdu. <a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Accept this secure invitation</a>. It expires in three days.</p>", $"Accept your GiddyEdu invitation within three days: {link}"));
        await notifications.EnqueueAsync("email", email, "identity.account-invitation", payload, ct);
        return new(invitation.Id, invitation.TargetType, invitation.TargetId, invitation.Email, invitation.ExpiresAtUtc);
    }

    public async Task<Guid> AcceptAsync(AcceptAccountInvitationInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Token)) throw new ArgumentException("Invitation token is required.", nameof(input));
        var invitation = await db.AccountInvitations.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TokenHash == Hash(input.Token), ct) ?? throw new KeyNotFoundException("Invitation was not found.");
        if (!invitation.IsUsable(clock.UtcNow)) throw new InvalidOperationException("Invitation is expired, revoked, or already accepted.");
        tenantSetter.Set(invitation.TenantId, null);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var email = invitation.Email.ToLowerInvariant();
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new PlatformUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, DisplayName = Required(input.DisplayName, 200), CreatedAtUtc = clock.UtcNow };
                var result = await users.CreateAsync(user, input.Password);
                if (!result.Succeeded) throw new ArgumentException(string.Join(" ", result.Errors.Select(x => x.Description)), nameof(input));
            }
            var membership = await db.TenantMemberships.SingleOrDefaultAsync(x => x.UserId == user.Id, ct);
            if (membership is null) { membership = new TenantMembership(Guid.NewGuid(), invitation.TenantId, user.Id, clock.UtcNow); db.TenantMemberships.Add(membership); }
            await LinkTargetAsync(invitation, user.Id, ct);
            var roleId = await EnsureRoleAsync(invitation.TargetType, ct);
            if (!await db.TenantMembershipRoles.AnyAsync(x => x.MembershipId == membership.Id && x.RoleId == roleId, ct)) db.TenantMembershipRoles.Add(new TenantMembershipRole(invitation.TenantId, membership.Id, roleId));
            invitation.Accept(clock.UtcNow); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return invitation.TenantId;
        }
        finally { tenantSetter.Clear(); }
    }

    private async Task LinkTargetAsync(AccountInvitation invitation, Guid userId, CancellationToken ct)
    {
        if (invitation.TargetType == InvitationTargetType.Staff)
        {
            var staff = await db.StaffProfiles.SingleOrDefaultAsync(x => x.Id == invitation.TargetId && x.UserId == null, ct) ?? throw new InvalidOperationException("Staff record is already linked or unavailable.");
            staff.LinkUser(userId, clock.UtcNow);
        }
        else
        {
            var guardian = await db.Guardians.SingleOrDefaultAsync(x => x.Id == invitation.TargetId && x.UserId == null, ct) ?? throw new InvalidOperationException("Guardian record is already linked or unavailable.");
            guardian.LinkUser(userId);
        }
    }

    private async Task<Guid> EnsureRoleAsync(InvitationTargetType targetType, CancellationToken ct)
    {
        var tenantId = tenant.TenantId!.Value; var name = targetType == InvitationTargetType.Staff ? "Staff" : "Guardian";
        var permissionNames = targetType == InvitationTargetType.Staff
            ? new[] { Permissions.SchoolsView, Permissions.AcademicsView, Permissions.StaffView, Permissions.StudentsView }
            : new[] { Permissions.StudentsView, Permissions.GuardiansView };
        var role = await db.TenantRoles.SingleOrDefaultAsync(x => x.Name == name, ct);
        if (role is null) { role = new TenantRole(Guid.NewGuid(), tenantId, name, true); db.TenantRoles.Add(role); }
        var permissionIds = await db.Permissions.Where(x => permissionNames.Contains(x.Name)).Select(x => x.Id).ToListAsync(ct);
        foreach (var permissionId in permissionIds)
            if (!await db.RolePermissions.AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permissionId, ct)) db.RolePermissions.Add(new RolePermission(tenantId, role.Id, permissionId));
        return role.Id;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value is required and must not exceed {max} characters.") : value.Trim();
}
