using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Messaging;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Schools.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

public static class AuthEndpoints
{
    public sealed record RegisterRequest(string SchoolName, string SchoolType, string RoleAtSchool, string Phone, string DisplayName, string Email, string Password, bool AcceptTerms, string TurnstileToken);
    public sealed record LoginRequest(string Email, string Password, Guid TenantId, Guid? CampusId);
    public sealed record PlatformLoginRequest(string Email, string Password);
    public sealed record WorkspaceDiscoveryRequest(string Email, string Password);
    public sealed record SwitchWorkspaceRequest(Guid TenantId, Guid? CampusId, string RefreshToken);
    public sealed record CampusWorkspace(Guid CampusId, string CampusName);
    public sealed record SchoolWorkspace(Guid TenantId, string TenantName, IReadOnlyList<CampusWorkspace> Campuses);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record ConfirmEmailRequest(string Email, string Token);
    public sealed record ConfirmEmailCodeRequest(string Email, string Code);
    public sealed record ResendEmailConfirmationRequest(string Email);
    public sealed record ForgotPasswordRequest(string Email, string TurnstileToken);
    public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
    public sealed record ResetPasswordCodeRequest(string Email, string Code, string NewPassword);
    private sealed record PasswordResetCode(string CodeHash, string Salt, int FailedAttempts);
    private sealed record TurnstileVerificationResponse(bool Success, string? Action, [property: System.Text.Json.Serialization.JsonPropertyName("error-codes")] string[]? ErrorCodes);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/platform/login", PlatformLoginAsync);
        group.MapPost("/workspaces", DiscoverWorkspacesAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/confirm-email", ConfirmEmailAsync);
        group.MapPost("/confirm-email/code", ConfirmEmailWithCodeAsync).RequireRateLimiting("account-recovery");
        group.MapPost("/confirm-email/resend", ResendEmailConfirmationAsync).RequireRateLimiting("account-recovery");
        group.MapPost("/forgot-password", ForgotPasswordAsync).RequireRateLimiting("account-recovery");
        group.MapPost("/reset-password", ResetPasswordAsync);
        group.MapPost("/reset-password/code", ResetPasswordWithCodeAsync).RequireRateLimiting("account-recovery");
        endpoints.MapPost("/api/v1/auth/switch-workspace", SwitchWorkspaceAsync).RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapGet("/api/v1/auth/workspaces", ListWorkspacesAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/auth/logout", LogoutAsync).RequireAuthorization().RequireRateLimiting("auth");
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, UserManager<PlatformUser> users, GiddyEduDbContext db, ITenantContextSetter tenant, INotificationQueue notifications, IConfiguration configuration, IConnectionMultiplexer redis, IHttpClientFactory httpClientFactory, HttpContext httpContext, IClock clock, CancellationToken cancellationToken)
    {
        if (!request.AcceptTerms) return Validation("You must agree to the Terms and Conditions and acknowledge the Privacy Policy.");
        if (!await VerifyTurnstileAsync(request.TurnstileToken, "register", httpContext.Connection.RemoteIpAddress?.ToString(), configuration, httpClientFactory, cancellationToken)) return Validation("Complete the security check and try again.");
        if (string.IsNullOrWhiteSpace(request.SchoolName) || string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Phone)) return Validation("Required registration fields are missing.");
        var schoolTypes = new[] { "Nursery", "Primary", "Secondary", "Primary and Secondary", "Nursery, Primary and Secondary" };
        var schoolRoles = new[] { "Owner / Proprietor", "Head Teacher / Principal", "Admin Officer", "Other" };
        if (!schoolTypes.Contains(request.SchoolType, StringComparer.Ordinal) || !schoolRoles.Contains(request.RoleAtSchool, StringComparer.Ordinal)) return Validation("Select a valid school type and role at the school.");
        if (request.Phone.Length != 11 || !request.Phone.All(char.IsDigit)) return Validation("Phone number must contain exactly 11 digits.");
        var normalizedEmail = request.Email.Trim();
        if (await users.FindByEmailAsync(normalizedEmail) is not null) return Results.Conflict(new { code = "email_exists", message = "An account already exists for this email address." });
        var normalizedSchoolName = request.SchoolName.Trim();
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Name.ToLower() == normalizedSchoolName.ToLower(), cancellationToken)) return Results.Conflict(new { code = "school_name_exists", message = "A school with this name already exists." });
        var slug = CreateSchoolSlug(normalizedSchoolName);
        if (slug.Length < 3) return Validation("School name must contain at least three letters or numbers.");
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Slug == slug, cancellationToken)) return Results.Conflict(new { code = "school_url_exists", message = "The generated school URL is already in use." });
        var user = new PlatformUser { Id = Guid.NewGuid(), UserName = normalizedEmail, Email = normalizedEmail, PhoneNumber = request.Phone.Trim(), DisplayName = request.DisplayName.Trim(), CreatedAtUtc = clock.UtcNow };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return IdentityErrors(created);

        var tenantId = Guid.NewGuid(); var membershipId = Guid.NewGuid(); var roleId = Guid.NewGuid();
        var registrationCommitted = false;
        tenant.Set(tenantId, null);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.Tenants.Add(new Tenant(tenantId, request.SchoolName, slug, clock.UtcNow));
            db.Campuses.Add(new Campus(Guid.NewGuid(), tenantId, "Main Campus", "MAIN", clock.UtcNow, true));
            db.TenantMemberships.Add(new TenantMembership(membershipId, tenantId, user.Id, clock.UtcNow, request.RoleAtSchool));
            db.SchoolProfiles.Add(new SchoolProfile(tenantId, request.SchoolName, request.SchoolType, request.SchoolName, "NG", "Africa/Lagos", "NGN", clock.UtcNow));
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

            await QueueEmailConfirmationAsync(user, users, notifications, configuration, redis, cancellationToken);
            return Results.Accepted(value: new { tenantId, email = user.Email, message = "Registration created. Enter the verification code sent to your email address." });
        }
        catch { if (!registrationCommitted) await users.DeleteAsync(user); throw; }
        finally { tenant.Clear(); }
    }

    private static async Task<bool> VerifyTurnstileAsync(string token, string expectedAction, string? remoteIpAddress, IConfiguration configuration, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var secretKey = configuration["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(token)) return false;
        var fields = new Dictionary<string, string> { ["secret"] = secretKey, ["response"] = token };
        if (!string.IsNullOrWhiteSpace(remoteIpAddress)) fields["remoteip"] = remoteIpAddress;
        using var content = new FormUrlEncodedContent(fields);
        using var response = await httpClientFactory.CreateClient().PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
        var verification = await response.Content.ReadFromJsonAsync<TurnstileVerificationResponse>(cancellationToken);
        return verification is { Success: true } && (string.IsNullOrWhiteSpace(verification.Action) || verification.Action == expectedAction);
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, UserManager<PlatformUser> users, GiddyEduDbContext db, ITenantContextSetter tenant, IConfiguration configuration, IClock clock, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
        if (!user.EmailConfirmed) return Results.Json(new { code = "email_unconfirmed", message = "Confirm your email address before signing in." }, statusCode: StatusCodes.Status403Forbidden);
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

    private static async Task<IResult> DiscoverWorkspacesAsync(WorkspaceDiscoveryRequest request, UserManager<PlatformUser> users, GiddyEduDbContext db, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
        if (!user.EmailConfirmed) return Results.Json(new { code = "email_unconfirmed", message = "Confirm your email address before signing in." }, statusCode: StatusCodes.Status403Forbidden);
        return Results.Ok(await LoadWorkspacesAsync(user.Id, db, cancellationToken));
    }

    private static async Task<IResult> ListWorkspacesAsync(ClaimsPrincipal principal, GiddyEduDbContext db, CancellationToken cancellationToken)
    {
        var userValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userValue, out var userId)) return Results.Forbid();
        return Results.Ok(await LoadWorkspacesAsync(userId, db, cancellationToken));
    }

    public static async Task<IReadOnlyList<SchoolWorkspace>> LoadWorkspacesAsync(Guid userId, GiddyEduDbContext db, CancellationToken cancellationToken)
    {
        var schools = await (from membership in db.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
                             join school in db.Tenants.IgnoreQueryFilters().AsNoTracking() on membership.TenantId equals school.Id
                             where membership.UserId == userId && membership.IsActive && school.IsActive
                             orderby school.Name
                             select new { school.Id, school.Name }).ToListAsync(cancellationToken);
        var tenantIds = schools.Select(x => x.Id).ToArray();
        var campuses = await db.Campuses.IgnoreQueryFilters().AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId) && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.TenantId, Workspace = new CampusWorkspace(x.Id, x.Name) })
            .ToListAsync(cancellationToken);

        return schools.Select(school => new SchoolWorkspace(
            school.Id,
            school.Name,
            campuses.Where(campus => campus.TenantId == school.Id).Select(campus => campus.Workspace).ToArray()))
            .ToArray();
    }

    private static async Task<IResult> SwitchWorkspaceAsync(SwitchWorkspaceRequest request, ClaimsPrincipal principal, GiddyEduDbContext db, ITenantContextSetter tenant, IConfiguration configuration, IClock clock, CancellationToken cancellationToken)
    {
        var userValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userValue, out var userId)) return Results.Forbid();
        var membership = await db.TenantMemberships.IgnoreQueryFilters().AnyAsync(x => x.TenantId == request.TenantId && x.UserId == userId && x.IsActive, cancellationToken);
        if (!membership) return Results.Forbid();
        if (request.CampusId.HasValue && !await db.Campuses.IgnoreQueryFilters().AnyAsync(x => x.TenantId == request.TenantId && x.Id == request.CampusId && x.IsActive, cancellationToken)) return Results.Forbid();
        var tokenHash = Hash(request.RefreshToken);
        var currentToken = await db.RefreshTokens.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserId == userId && x.TokenHash == tokenHash, cancellationToken);
        if (currentToken is null || !currentToken.IsUsable(clock.UtcNow)) return Results.Unauthorized();
        currentToken.Revoke(clock.UtcNow);
        tenant.Set(request.TenantId, request.CampusId);
        try { return Results.Ok(await IssueTokensAsync(userId, request.TenantId, request.CampusId, db, configuration, clock, cancellationToken)); }
        finally { tenant.Clear(); }
    }

    private static async Task<IResult> LogoutAsync(RefreshRequest request, ClaimsPrincipal principal, GiddyEduDbContext db, IClock clock, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Forbid();
        var hash = Hash(request.RefreshToken);
        var tenantToken = await db.RefreshTokens.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TokenHash == hash && x.UserId == userId, cancellationToken);
        if (tenantToken is not null) tenantToken.Revoke(clock.UtcNow);
        var platformToken = await db.PlatformRefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash && x.UserId == userId, cancellationToken);
        if (platformToken is not null) platformToken.Revoke(clock.UtcNow);
        if (tenantToken is not null || platformToken is not null) await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
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

    private static async Task<IResult> ConfirmEmailAsync(ConfirmEmailRequest request, UserManager<PlatformUser> users, GiddyEduDbContext db, ITenantContextSetter tenant, INotificationQueue notifications, IConfiguration configuration, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim()); if (user is null) return Results.BadRequest();
        var result = await users.ConfirmEmailAsync(user, Decode(request.Token));
        if (!result.Succeeded) return IdentityErrors(result);
        await QueueWelcomeEmailAsync(user, db, tenant, notifications, configuration, loggerFactory, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmEmailWithCodeAsync(ConfirmEmailCodeRequest request, UserManager<PlatformUser> users, GiddyEduDbContext db, ITenantContextSetter tenant, INotificationQueue notifications, IConfiguration configuration, ILoggerFactory loggerFactory, IConnectionMultiplexer redis, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code) || request.Code.Length != 6 || request.Code.Any(c => !char.IsAsciiDigit(c))) return Results.BadRequest();
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not { IsActive: true } || user.EmailConfirmed) return Results.BadRequest();
        var database = redis.GetDatabase(); var key = EmailConfirmationCodeKey(user.Email!); var storedValue = await database.StringGetAsync(key);
        if (!storedValue.HasValue) return Results.BadRequest();
        var stored = JsonSerializer.Deserialize<PasswordResetCode>(storedValue.ToString());
        if (stored is null) { await database.KeyDeleteAsync(key); return Results.BadRequest(); }
        var suppliedHash = Convert.FromBase64String(HashResetCode(request.Code, Convert.FromBase64String(stored.Salt)));
        var expectedHash = Convert.FromBase64String(stored.CodeHash);
        if (!CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash))
        {
            var remaining = await database.KeyTimeToLiveAsync(key);
            if (stored.FailedAttempts >= 4 || remaining is null) await database.KeyDeleteAsync(key);
            else await database.StringSetAsync(key, JsonSerializer.Serialize(stored with { FailedAttempts = stored.FailedAttempts + 1 }), remaining);
            return Results.BadRequest();
        }
        var result = await users.ConfirmEmailAsync(user, await users.GenerateEmailConfirmationTokenAsync(user));
        if (!result.Succeeded) return IdentityErrors(result);
        await database.KeyDeleteAsync(key);
        await QueueWelcomeEmailAsync(user, db, tenant, notifications, configuration, loggerFactory, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ResendEmailConfirmationAsync(ResendEmailConfirmationRequest request, UserManager<PlatformUser> users, GiddyEduDbContext db, ITenantContextSetter tenant, INotificationQueue notifications, IConfiguration configuration, IConnectionMultiplexer redis, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) return Results.Accepted();
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not { IsActive: true, EmailConfirmed: false }) return Results.Accepted();
        var tenantId = await db.TenantMemberships.IgnoreQueryFilters().AsNoTracking().Where(x => x.UserId == user.Id && x.IsActive).OrderBy(x => x.CreatedAtUtc).Select(x => (Guid?)x.TenantId).FirstOrDefaultAsync(cancellationToken);
        if (tenantId.HasValue)
        {
            tenant.Set(tenantId.Value, null);
            try { await QueueEmailConfirmationAsync(user, users, notifications, configuration, redis, cancellationToken); }
            finally { tenant.Clear(); }
        }
        return Results.Accepted();
    }

    private static async Task QueueEmailConfirmationAsync(PlatformUser user, UserManager<PlatformUser> users, INotificationQueue notifications, IConfiguration configuration, IConnectionMultiplexer redis, CancellationToken cancellationToken)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture); var salt = RandomNumberGenerator.GetBytes(16);
        await redis.GetDatabase().StringSetAsync(EmailConfirmationCodeKey(user.Email!), JsonSerializer.Serialize(new PasswordResetCode(HashResetCode(code, salt), Convert.ToBase64String(salt), 0)), TimeSpan.FromMinutes(10));
        var link = BuildLink(configuration, "confirm-email", user.Email!, Encode(await users.GenerateEmailConfirmationTokenAsync(user)));
        var content = GiddyEduEmailTemplate.Create("Confirm your email address", "Welcome to GiddyEdu. Enter this code to activate your secure school account.", "Confirm with secure link", link, code, "This code expires in 10 minutes and can be used only once.");
        var payload = JsonSerializer.Serialize(new EmailNotificationPayload("Confirm your GiddyEdu account", content.HtmlBody, content.TextBody));
        await notifications.EnqueueAsync("email", user.Email!, "identity.confirm-email", payload, cancellationToken);
    }

    private static async Task QueueWelcomeEmailAsync(PlatformUser user, GiddyEduDbContext db, ITenantContextSetter tenant, INotificationQueue notifications, IConfiguration configuration, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var tenantId = await db.TenantMemberships.IgnoreQueryFilters().AsNoTracking().Where(x => x.UserId == user.Id && x.IsActive).OrderBy(x => x.CreatedAtUtc).Select(x => (Guid?)x.TenantId).FirstOrDefaultAsync(cancellationToken);
        if (!tenantId.HasValue) return;
        tenant.Set(tenantId.Value, null);
        try { var signInUrl = $"{configuration["App:PublicBaseUrl"]?.TrimEnd('/') ?? "http://localhost:3000"}/login"; var content = GiddyEduEmailTemplate.Create("Welcome to GiddyEdu", $"Hello {user.DisplayName}, your email address is confirmed and your secure school workspace is ready.", "Sign in to GiddyEdu", signInUrl, supportingText: "You can now continue your school setup, invite your team and manage the workspaces connected to your account."); var payload = JsonSerializer.Serialize(new EmailNotificationPayload("Welcome to GiddyEdu", content.HtmlBody, content.TextBody)); await notifications.EnqueueAsync("email", user.Email!, "identity.welcome", payload, cancellationToken); }
        catch (Exception exception) { loggerFactory.CreateLogger("GiddyEdu.Identity.EmailConfirmation").LogWarning(exception, "Welcome email could not be queued for user {UserId} after successful confirmation.", user.Id); }
        finally { tenant.Clear(); }
    }

    private static async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request, UserManager<PlatformUser> users, IEmailSender email, IConfiguration configuration, IConnectionMultiplexer redis, IHttpClientFactory httpClientFactory, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) return Validation("Enter your account email address.");
        if (!await VerifyTurnstileAsync(request.TurnstileToken, "forgot-password", httpContext.Connection.RemoteIpAddress?.ToString(), configuration, httpClientFactory, cancellationToken)) return Validation("Complete the security check and try again.");
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not { IsActive: true }) return Results.NotFound(new { code = "account_email_not_found", message = "No active GiddyEdu account was found for this email address." });
        var stampResult = await users.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded) throw new InvalidOperationException("Password reset could not be prepared.");
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var salt = RandomNumberGenerator.GetBytes(16);
        var resetCode = new PasswordResetCode(HashResetCode(code, salt), Convert.ToBase64String(salt), 0);
        await redis.GetDatabase().StringSetAsync(PasswordResetCodeKey(user.Email!), JsonSerializer.Serialize(resetCode), TimeSpan.FromMinutes(10));
        var link = BuildLink(configuration, "reset-password", user.Email!, Encode(await users.GeneratePasswordResetTokenAsync(user)));
        var content = GiddyEduEmailTemplate.Create("Reset your password", "We received a request to reset the password for your GiddyEdu account.", "Reset password securely", link, code, "This code expires in 10 minutes and can be used only once. If you did not request this, you can safely ignore this email.");
        await email.SendAsync(user.Email!, "Your GiddyEdu password reset code", content.HtmlBody, content.TextBody, cancellationToken);
        return Results.Accepted();
    }

    private static async Task<IResult> ResetPasswordWithCodeAsync(ResetPasswordCodeRequest request, UserManager<PlatformUser> users, IConnectionMultiplexer redis)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code) || request.Code.Length != 6 || request.Code.Any(c => !char.IsAsciiDigit(c))) return Results.BadRequest();
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not { IsActive: true }) return Results.BadRequest();
        var database = redis.GetDatabase();
        var key = PasswordResetCodeKey(user.Email!);
        var storedValue = await database.StringGetAsync(key);
        if (!storedValue.HasValue) return Results.BadRequest();
        var stored = JsonSerializer.Deserialize<PasswordResetCode>(storedValue.ToString());
        if (stored is null) { await database.KeyDeleteAsync(key); return Results.BadRequest(); }
        var suppliedHash = Convert.FromBase64String(HashResetCode(request.Code, Convert.FromBase64String(stored.Salt)));
        var expectedHash = Convert.FromBase64String(stored.CodeHash);
        if (!CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash))
        {
            var remaining = await database.KeyTimeToLiveAsync(key);
            if (stored.FailedAttempts >= 4 || remaining is null) await database.KeyDeleteAsync(key);
            else await database.StringSetAsync(key, JsonSerializer.Serialize(stored with { FailedAttempts = stored.FailedAttempts + 1 }), remaining);
            return Results.BadRequest();
        }
        var result = await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), request.NewPassword);
        if (!result.Succeeded) return IdentityErrors(result);
        if (!user.EmailConfirmed) { user.EmailConfirmed = true; var confirmation = await users.UpdateAsync(user); if (!confirmation.Succeeded) return IdentityErrors(confirmation); }
        await database.KeyDeleteAsync(key);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request, UserManager<PlatformUser> users)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim()); if (user is null) return Results.BadRequest();
        var result = await users.ResetPasswordAsync(user, Decode(request.Token), request.NewPassword);
        return result.Succeeded ? Results.NoContent() : IdentityErrors(result);
    }

    private static string PasswordResetCodeKey(string email) => $"identity:password-reset:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant())))}";
    private static string CreateSchoolSlug(string schoolName)
    {
        var slug = new string(string.Join('-', schoolName.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Select(character => char.IsAsciiLetterOrDigit(character) || character == '-' ? character : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        return slug[..Math.Min(slug.Length, 100)].TrimEnd('-');
    }
    private static string EmailConfirmationCodeKey(string email) => $"identity:email-confirmation:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant())))}";
    private static string HashResetCode(string code, byte[] salt) => Convert.ToBase64String(SHA256.HashData([.. salt, .. Encoding.UTF8.GetBytes(code)]));

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
