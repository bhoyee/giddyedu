using System.Security.Claims;
using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Infrastructure.Subscriptions;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;

public static class FoundationEndpoints
{
    public sealed record CreateRoleRequest(string Name, Guid[] PermissionIds);
    public sealed record AssignRoleRequest(Guid MembershipId, Guid RoleId);
    public sealed record SettingRequest(JsonElement Value, Guid? CampusId);
    public sealed record BeginFileUploadRequest(string FileName, string ContentType, long SizeBytes, string Category, string EntityType, Guid EntityId);
    public sealed record CompleteFileUploadRequest(string Checksum);

    public static IEndpointRouteBuilder MapFoundationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/foundation");
        group.MapGet("/entitlements/{featureKey}", GetEntitlementAsync);
        group.MapGet("/flags/{key}", async (string key, IFeatureFlagService flags, CancellationToken ct) => Results.Ok(new { key, enabled = await flags.IsEnabledAsync(key, ct) }));
        group.MapGet("/settings/{key}", GetSettingAsync);
        group.MapPut("/settings/{key}", PutSettingAsync);
        group.MapPost("/roles", CreateRoleAsync);
        group.MapPost("/role-assignments", AssignRoleAsync);
        group.MapPost("/files", BeginFileAsync);
        group.MapPost("/files/{fileId:guid}/complete", CompleteFileAsync);
        group.MapGet("/files/{fileId:guid}/download", DownloadFileAsync);
        group.MapDelete("/files/{fileId:guid}", DeleteFileAsync);
        return endpoints;
    }

    private static async Task<IResult> GetEntitlementAsync(string featureKey, Guid? campusId, IEntitlementService service, CancellationToken ct) => Results.Ok(await service.GetAsync(featureKey, campusId, ct));
    private static async Task<IResult> GetSettingAsync(string key, Guid? campusId, ISettingsService settings, CancellationToken ct)
    {
        var value = await settings.GetAsync<JsonElement?>(key, campusId, ct); return value.HasValue ? Results.Ok(value.Value) : Results.NotFound();
    }
    private static async Task<IResult> PutSettingAsync(string key, SettingRequest request, ClaimsPrincipal principal, IPermissionService permissions, ITenantContext tenant, GiddyEduDbContext db, IClock clock, IAuditWriter audit, CancellationToken ct)
    {
        var userId = UserId(principal); if (!await permissions.HasPermissionAsync(userId, Permissions.TenantSettingsManage, ct)) return Results.Forbid();
        var tenantId = tenant.TenantId!.Value; var json = request.Value.GetRawText();
        if (request.CampusId.HasValue)
        {
            if (!await db.Campuses.AnyAsync(x => x.Id == request.CampusId && x.IsActive, ct)) return Results.NotFound();
            var setting = await db.CampusSettings.SingleOrDefaultAsync(x => x.CampusId == request.CampusId && x.Key == key, ct);
            if (setting is null) db.CampusSettings.Add(new CampusSetting(tenantId, request.CampusId.Value, key, json, clock.UtcNow)); else setting.Update(json, clock.UtcNow);
        }
        else
        {
            var setting = await db.TenantSettings.SingleOrDefaultAsync(x => x.Key == key, ct);
            if (setting is null) db.TenantSettings.Add(new TenantSetting(tenantId, key, json, clock.UtcNow, userId)); else setting.Update(json, clock.UtcNow, userId);
        }
        await db.SaveChangesAsync(ct); await audit.WriteAsync(userId, "Setting.Upsert", request.CampusId.HasValue ? "CampusSetting" : "TenantSetting", key, "Succeeded", null, ct); return Results.NoContent();
    }
    private static async Task<IResult> CreateRoleAsync(CreateRoleRequest request, ClaimsPrincipal principal, TenantRoleService roles, IAuditWriter audit, CancellationToken ct)
    {
        var userId = UserId(principal); var id = await roles.CreateRoleAsync(userId, request.Name, request.PermissionIds, ct); await audit.WriteAsync(userId, "Role.Create", "TenantRole", id.ToString(), "Succeeded", null, ct); return Results.Created($"/api/v1/foundation/roles/{id}", new { id });
    }
    private static async Task<IResult> AssignRoleAsync(AssignRoleRequest request, ClaimsPrincipal principal, TenantRoleService roles, IAuditWriter audit, CancellationToken ct)
    {
        var userId = UserId(principal); await roles.AssignRoleAsync(userId, request.MembershipId, request.RoleId, ct); await audit.WriteAsync(userId, "Role.Assign", "TenantMembership", request.MembershipId.ToString(), "Succeeded", null, ct); return Results.NoContent();
    }
    private static async Task<IResult> BeginFileAsync(BeginFileUploadRequest request, ClaimsPrincipal principal, IPermissionService permissions, IFileService files, IAuditWriter audit, CancellationToken ct)
    {
        var userId = UserId(principal); if (!await permissions.HasPermissionAsync(userId, Permissions.FilesManage, ct)) return Results.Forbid();
        var upload = await files.BeginUploadAsync(request.FileName, request.ContentType, request.SizeBytes, request.Category, request.EntityType, request.EntityId, userId, ct); await audit.WriteAsync(userId, "File.BeginUpload", "StoredFile", upload.FileId.ToString(), "Succeeded", null, ct); return Results.Created($"/api/v1/foundation/files/{upload.FileId}", upload);
    }
    private static async Task<IResult> CompleteFileAsync(Guid fileId, CompleteFileUploadRequest request, ClaimsPrincipal principal, IPermissionService permissions, IFileService files, IAuditWriter audit, CancellationToken ct)
    {
        var userId = UserId(principal); if (!await permissions.HasPermissionAsync(userId, Permissions.FilesManage, ct)) return Results.Forbid(); await files.CompleteUploadAsync(fileId, request.Checksum, ct); await audit.WriteAsync(userId, "File.CompleteUpload", "StoredFile", fileId.ToString(), "Succeeded", null, ct); return Results.NoContent();
    }
    private static async Task<IResult> DownloadFileAsync(Guid fileId, ClaimsPrincipal principal, IPermissionService permissions, IFileService files, IAuditWriter audit, CancellationToken ct)
    {
        var userId = UserId(principal); if (!await permissions.HasPermissionAsync(userId, Permissions.FilesManage, ct)) return Results.Forbid(); var url = await files.CreateDownloadUrlAsync(fileId, ct); await audit.WriteAsync(userId, "File.Download", "StoredFile", fileId.ToString(), "Succeeded", null, ct); return Results.Ok(new { url });
    }
    private static async Task<IResult> DeleteFileAsync(Guid fileId, ClaimsPrincipal principal, IPermissionService permissions, IFileService files, IAuditWriter audit, CancellationToken ct)
    {
        var userId = UserId(principal); if (!await permissions.HasPermissionAsync(userId, Permissions.FilesManage, ct)) return Results.Forbid(); await files.DeleteAsync(fileId, ct); await audit.WriteAsync(userId, "File.Delete", "StoredFile", fileId.ToString(), "Succeeded", null, ct); return Results.NoContent();
    }
    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
}
