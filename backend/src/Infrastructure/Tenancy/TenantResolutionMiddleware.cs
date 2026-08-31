using System.Security.Claims;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Tenancy;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextSetter tenantSetter, GiddyEduDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var tenantValue = context.User.FindFirstValue("tenant_id");
        var userValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var campusValue = context.User.FindFirstValue("campus_id");
        if (!Guid.TryParse(tenantValue, out var tenantId) || !Guid.TryParse(userValue, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        tenantSetter.Set(tenantId, Guid.TryParse(campusValue, out var campusId) ? campusId : null);
        var isMember = await dbContext.TenantMemberships.AnyAsync(x => x.UserId == userId && x.IsActive, context.RequestAborted);
        if (!isMember)
        {
            tenantSetter.Clear();
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        try { await next(context); }
        finally { tenantSetter.Clear(); }
    }
}
