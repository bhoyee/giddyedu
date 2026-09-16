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
using GiddyEdu.Modules.Platform.Domain;
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
            InvitationTargetType.Student => await db.Students.Where(x => x.Id == input.TargetId && x.UserId == null).Select(x => x.Email).SingleOrDefaultAsync(ct),
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
        var schoolName = await db.SchoolProfiles.Where(x => x.TenantId == tenantId).Select(x => x.DisplayName).SingleOrDefaultAsync(ct)
            ?? await db.Tenants.Where(x => x.Id == tenantId).Select(x => x.Name).SingleAsync(ct);
        var campusName = input.TargetType == InvitationTargetType.Staff
            ? await db.StaffProfiles.Where(x => x.Id == input.TargetId).Join(db.Campuses, staff => staff.CampusId, campus => campus.Id, (_, campus) => campus.Name).SingleAsync(ct)
            : null;
        var isStaff = input.TargetType == InvitationTargetType.Staff;
        var message = isStaff
            ? $"{schoolName} has created your staff workspace for {campusName}. Your sign-in email is {email}. Use the secure invitation below to choose your password before signing in."
            : $"{schoolName} has invited you to join its secure school workspace.";
        var content = GiddyEduEmailTemplate.Create(isStaff ? "Welcome to your staff workspace" : "You’re invited to GiddyEdu", message,
            isStaff ? "Set up your staff account" : "Accept invitation", link,
            supportingText: "This invitation expires in three days. If you were not expecting it, you can safely ignore this email.",
            organizationName: schoolName, secondaryActionLabel: isStaff ? "Sign-in page" : null,
            secondaryActionUrl: isStaff ? $"{baseUrl}/login" : null);
        var payload = JsonSerializer.Serialize(new EmailNotificationPayload(isStaff ? $"{schoolName} | Set up your staff account" : $"{schoolName} | Your invitation", content.HtmlBody, content.TextBody));
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
            invitation.Accept(clock.UtcNow);
            db.AuditRecords.Add(new AuditRecord(Guid.NewGuid(), invitation.TenantId, user.Id, "AccountInvitation.Accept", "AccountInvitation", invitation.Id.ToString(), "Succeeded", clock.UtcNow, JsonSerializer.Serialize(new { invitation.TargetType, invitation.TargetId })));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return invitation.TenantId;
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
        else if (invitation.TargetType == InvitationTargetType.Guardian)
        {
            var guardian = await db.Guardians.SingleOrDefaultAsync(x => x.Id == invitation.TargetId && x.UserId == null, ct) ?? throw new InvalidOperationException("Guardian record is already linked or unavailable.");
            guardian.LinkUser(userId);
        }
        else if (invitation.TargetType == InvitationTargetType.Student)
        {
            var student = await db.Students.SingleOrDefaultAsync(x => x.Id == invitation.TargetId && x.UserId == null, ct) ?? throw new InvalidOperationException("Student record is already linked or unavailable.");
            student.LinkUser(userId);
        }
        else throw new InvalidOperationException("Invitation target type is unsupported.");
    }

    private async Task<Guid> EnsureRoleAsync(InvitationTargetType targetType, CancellationToken ct)
    {
        var tenantId = tenant.TenantId!.Value;
        var template = targetType switch
        {
            InvitationTargetType.Staff => SystemRoleTemplates.Staff,
            InvitationTargetType.Guardian => SystemRoleTemplates.Parent,
            InvitationTargetType.Student => SystemRoleTemplates.Student,
            _ => throw new InvalidOperationException("Invitation target type is unsupported.")
        };
        var role = await db.TenantRoles.SingleOrDefaultAsync(x => x.Name == template.Name, ct);
        if (role is null) { role = new TenantRole(Guid.NewGuid(), tenantId, template.Name, true); db.TenantRoles.Add(role); }
        var permissionIds = await db.Permissions.Where(x => template.Permissions.Contains(x.Name)).Select(x => x.Id).ToListAsync(ct);
        foreach (var permissionId in permissionIds)
            if (!await db.RolePermissions.AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permissionId, ct)) db.RolePermissions.Add(new RolePermission(tenantId, role.Id, permissionId));
        return role.Id;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value is required and must not exceed {max} characters.") : value.Trim();
}
