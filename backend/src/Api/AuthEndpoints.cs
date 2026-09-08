using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Messaging;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public static class AuthEndpoints
{
    public sealed record RegisterRequest(string SchoolName, string SchoolSlug, string CampusName, string DisplayName, string Email, string Password);
    public sealed record LoginRequest(string Email, string Password, Guid TenantId, Guid? CampusId);
    public sealed record PlatformLoginRequest(string Email, string Password);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record ConfirmEmailRequest(string Email, string Token);
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/platform/login", PlatformLoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/confirm-email", ConfirmEmailAsync);
        group.MapPost("/forgot-password", ForgotPasswordAsync);
        group.MapPost("/reset-password", ResetPasswordAsync);
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, UserManager<PlatformUser> users, GiddyEduDbContext db, ITenantContextSetter tenant, INotificationQueue notifications, IConfiguration configuration, IClock clock, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SchoolName) || string.IsNullOrWhiteSpace(request.CampusName) || string.IsNullOrWhiteSpace(request.DisplayName)) return Validation("Required registration fields are missing.");
        var slug = request.SchoolSlug.Trim().ToLowerInvariant();
        if (slug.Length is < 3 or > 100 || slug.Any(c => !char.IsLetterOrDigit(c) && c != '-')) return Validation("SchoolSlug must contain only lowercase letters, numbers, and hyphens.");
        if (await db.Tenants.AnyAsync(x => x.Slug == slug, cancellationToken)) return Results.Conflict(new { code = "tenant_slug_exists" });
        var user = new PlatformUser { Id = Guid.NewGuid(), UserName = request.Email.Trim(), Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim(), CreatedAtUtc = clock.UtcNow };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return IdentityErrors(created);

        var tenantId = Guid.NewGuid(); var membershipId = Guid.NewGuid(); var roleId = Guid.NewGuid();
        var registrationCommitted = false;
        tenant.Set(tenantId, null);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.Tenants.Add(new Tenant(tenantId, request.SchoolName, slug, clock.UtcNow));
            db.Campuses.Add(new Campus(Guid.NewGuid(), tenantId, request.CampusName, "MAIN", clock.UtcNow));
            db.TenantMemberships.Add(new TenantMembership(membershipId, tenantId, user.Id, clock.UtcNow));
            var catalog = Permissions.Foundation;
            foreach (var name in catalog)
                if (!await db.Permissions.AnyAsync(x => x.Name == name, cancellationToken)) db.Permissions.Add(new Permission(Guid.NewGuid(), name, name));
            await db.SaveChangesAsync(cancellationToken);
            var permissionIds = await db.Permissions.Where(x => catalog.Contains(x.Name)).Select(x => x.Id).ToListAsync(cancellationToken);
            db.TenantRoles.Add(new TenantRole(roleId, tenantId, "Tenant Administrator", true));
            db.RolePermissions.AddRange(permissionIds.Select(x => new RolePermission(tenantId, roleId, x)));
            db.TenantMembershipRoles.Add(new TenantMembershipRole(tenantId, membershipId, roleId));
            var permissionMap = await db.Permissions.Where(x => catalog.Contains(x.Name)).ToDictionaryAsync(x => x.Name, x => x.Id, cancellationToken);
            foreach (var template in SystemRoleTemplates.TenantDefaults)
            {
                var templateRoleId = Guid.NewGuid(); db.TenantRoles.Add(new TenantRole(templateRoleId, tenantId, template.Name, true));
                db.RolePermissions.AddRange(template.Permissions.Where(permissionMap.ContainsKey).Select(name => new RolePermission(tenantId, templateRoleId, permissionMap[name])));
            }
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); registrationCommitted = true;

            var token = Encode(await users.GenerateEmailConfirmationTokenAsync(user));
            var link = BuildLink(configuration, "confirm-email", user.Email!, token);
            var payload = System.Text.Json.JsonSerializer.Serialize(new EmailNotificationPayload("Confirm your GiddyEdu account", $"<p>Confirm your account using <a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">this secure link</a>.</p>", $"Confirm your account: {link}"));
            await notifications.EnqueueAsync("email", user.Email!, "identity.confirm-email", payload, cancellationToken);
            return Results.Accepted(value: new { tenantId, message = "Registration created. Confirm the email address before signing in." });
        }
        catch { if (!registrationCommitted) await users.DeleteAsync(user); throw; }
        finally { tenant.Clear(); }
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, UserManager<PlatformUser> users, GiddyEduDbContext db, ITenantContextSetter tenant, IConfiguration configuration, IClock clock, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !user.EmailConfirmed || !await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
        tenant.Set(request.TenantId, request.CampusId);
        try
        {
            if (!await db.TenantMemberships.AnyAsync(x => x.UserId == user.Id && x.IsActive, cancellationToken)) return Results.Forbid();
            if (request.CampusId.HasValue && !await db.Campuses.AnyAsync(x => x.Id == request.CampusId && x.IsActive, cancellationToken)) return Results.Forbid();
            return Results.Ok(await IssueTokensAsync(user.Id, request.TenantId, request.CampusId, db, configuration, clock, cancellationToken));
        }
        finally { tenant.Clear(); }
    }

    private static async Task<IResult> PlatformLoginAsync(PlatformLoginRequest request, UserManager<PlatformUser> users, GiddyEduDbContext db, IConfiguration configuration, IClock clock, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !user.EmailConfirmed || !await users.CheckPasswordAsync(user, request.Password) || !await users.IsInRoleAsync(user, GlobalRoles.PlatformAdministrator))
            return Results.Unauthorized();
        return Results.Ok(await IssuePlatformTokensAsync(user.Id, db, configuration, clock, cancellationToken));
    }

    private static async Task<IResult> RefreshAsync(RefreshRequest request, GiddyEduDbContext db, ITenantContextSetter tenant, IConfiguration configuration, IClock clock, CancellationToken cancellationToken)
    {
        var tokenHash = Hash(request.RefreshToken);
        var platformToken = await db.PlatformRefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (platformToken is not null)
        {
            if (!platformToken.IsUsable(clock.UtcNow) || !await IsPlatformAdministratorAsync(platformToken.UserId, db, cancellationToken)) return Results.Unauthorized();
            platformToken.Revoke(clock.UtcNow);
            return Results.Ok(await IssuePlatformTokensAsync(platformToken.UserId, db, configuration, clock, cancellationToken));
        }
        var existing = await db.RefreshTokens.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (existing is null || !existing.IsUsable(clock.UtcNow)) return Results.Unauthorized();
        tenant.Set(existing.TenantId, existing.CampusId);
        try { existing.Revoke(clock.UtcNow); return Results.Ok(await IssueTokensAsync(existing.UserId, existing.TenantId, existing.CampusId, db, configuration, clock, cancellationToken)); }
        finally { tenant.Clear(); }
    }

    private static async Task<IResult> ConfirmEmailAsync(ConfirmEmailRequest request, UserManager<PlatformUser> users)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim()); if (user is null) return Results.BadRequest();
        var result = await users.ConfirmEmailAsync(user, Decode(request.Token));
        return result.Succeeded ? Results.NoContent() : IdentityErrors(result);
    }

    private static async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request, UserManager<PlatformUser> users, IEmailSender email, IConfiguration configuration)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null && user.EmailConfirmed)
        {
            var stampResult = await users.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded) throw new InvalidOperationException("Password reset could not be prepared.");
            var link = BuildLink(configuration, "reset-password", user.Email!, Encode(await users.GeneratePasswordResetTokenAsync(user)));
            await email.SendAsync(user.Email!, "Reset your GiddyEdu password", $"<p>Reset your password using <a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">this secure link</a>.</p>", $"Reset your password: {link}");
        }
        return Results.Accepted();
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request, UserManager<PlatformUser> users)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim()); if (user is null) return Results.BadRequest();
        var result = await users.ResetPasswordAsync(user, Decode(request.Token), request.NewPassword);
        return result.Succeeded ? Results.NoContent() : IdentityErrors(result);
    }

    private static async Task<object> IssueTokensAsync(Guid userId, Guid tenantId, Guid? campusId, GiddyEduDbContext db, IConfiguration configuration, IClock clock, CancellationToken cancellationToken)
    {
        var issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT issuer is required."); var audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT audience is required."); var key = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is required.");
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()), new("tenant_id", tenantId.ToString()) }; if (campusId.HasValue) claims.Add(new("campus_id", campusId.Value.ToString()));
        var globalRoles = await (from assignment in db.UserRoles join role in db.Roles on assignment.RoleId equals role.Id where assignment.UserId == userId select role.Name).ToListAsync(cancellationToken);
        claims.AddRange(globalRoles.Where(x => x is not null).Select(x => new Claim(ClaimTypes.Role, x!)));
        var expires = clock.UtcNow.AddMinutes(15); var jwt = new JwtSecurityToken(issuer, audience, claims, expires: expires.UtcDateTime, signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        var rawRefresh = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48)); db.RefreshTokens.Add(new RefreshToken(Guid.NewGuid(), tenantId, userId, campusId, Hash(rawRefresh), clock.UtcNow, clock.UtcNow.AddDays(14))); await db.SaveChangesAsync(cancellationToken);
        return new { accessToken = new JwtSecurityTokenHandler().WriteToken(jwt), expiresAtUtc = expires, refreshToken = rawRefresh };
    }

    private static async Task<object> IssuePlatformTokensAsync(Guid userId, GiddyEduDbContext db, IConfiguration configuration, IClock clock, CancellationToken cancellationToken)
    {
        var issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT issuer is required.");
        var audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT audience is required.");
        var key = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is required.");
        var expires = clock.UtcNow.AddMinutes(15);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, GlobalRoles.PlatformAdministrator) };
        var jwt = new JwtSecurityToken(issuer, audience, claims, expires: expires.UtcDateTime, signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        var rawRefresh = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        db.PlatformRefreshTokens.Add(new PlatformRefreshToken(Guid.NewGuid(), userId, Hash(rawRefresh), clock.UtcNow, clock.UtcNow.AddDays(14)));
        await db.SaveChangesAsync(cancellationToken);
        return new { accessToken = new JwtSecurityTokenHandler().WriteToken(jwt), expiresAtUtc = expires, refreshToken = rawRefresh };
    }

    private static Task<bool> IsPlatformAdministratorAsync(Guid userId, GiddyEduDbContext db, CancellationToken cancellationToken) =>
        (from assignment in db.UserRoles join role in db.Roles on assignment.RoleId equals role.Id where assignment.UserId == userId && role.Name == GlobalRoles.PlatformAdministrator select role.Id).AnyAsync(cancellationToken);

    private static string BuildLink(IConfiguration configuration, string path, string email, string token) => $"{configuration["App:PublicBaseUrl"]?.TrimEnd('/') ?? "http://localhost:3000"}/{path}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    private static string Decode(string token) => Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static IResult Validation(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });
    private static IResult IdentityErrors(IdentityResult result) => Results.ValidationProblem(result.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(e => e.Description).ToArray()));
}
