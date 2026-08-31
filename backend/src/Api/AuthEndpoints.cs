using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public static class AuthEndpoints
{
    public sealed record LoginRequest(string Email, string Password, Guid TenantId, Guid? CampusId);
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints) { endpoints.MapPost("/api/v1/auth/login", LoginAsync).AllowAnonymous(); return endpoints; }
    private static async Task<IResult> LoginAsync(LoginRequest request, UserManager<PlatformUser> users, GiddyEduDbContext db, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
        var membership = await db.TenantMemberships.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TenantId == request.TenantId && x.UserId == user.Id && x.IsActive, cancellationToken);
        if (membership is null) return Results.Forbid();
        if (request.CampusId.HasValue && !await db.Campuses.IgnoreQueryFilters().AnyAsync(x => x.Id == request.CampusId && x.TenantId == request.TenantId && x.IsActive, cancellationToken)) return Results.Forbid();
        var issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT issuer is required.");
        var audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT audience is required.");
        var signingKey = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is required.");
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new("tenant_id", request.TenantId.ToString()) };
        if (request.CampusId.HasValue) claims.Add(new Claim("campus_id", request.CampusId.Value.ToString()));
        var expires = DateTime.UtcNow.AddMinutes(30);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expires, signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256));
        return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc = expires });
    }
}
