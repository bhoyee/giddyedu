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
    public sealed record RenameRoleRequest(string Name);
    public sealed record ReplaceRolePermissionsRequest(Guid[] PermissionIds);
    public sealed record AssignRoleRequest(Guid MembershipId, Guid RoleId);
    public sealed record SettingRequest(JsonElement Value, Guid? CampusId);
    public sealed record BeginFileUploadRequest(string FileName, string ContentType, long SizeBytes, string Category, string EntityType, Guid EntityId);
    public sealed record CompleteFileUploadRequest(string Checksum);
    public sealed record CustomFieldValueRequest(JsonElement Value);

    public static IEndpointRouteBuilder MapFoundationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/foundation");
        group.MapGet("/entitlements/{featureKey}", GetEntitlementAsync);
        group.MapGet("/flags/{key}", async (string key, IFeatureFlagService flags, CancellationToken ct) => Results.Ok(new { key, enabled = await flags.IsEnabledAsync(key, ct) }));
        group.MapGet("/settings/{key}", GetSettingAsync);
        group.MapPut("/settings/{key}", PutSettingAsync);
        group.MapPost("/roles", CreateRoleAsync);
        group.MapGet("/permissions", ListPermissionsAsync);
        group.MapGet("/roles", ListRolesAsync);
        group.MapPut("/roles/{roleId:guid}", RenameRoleAsync);
        group.MapPut("/roles/{roleId:guid}/permissions", ReplaceRolePermissionsAsync);
        group.MapDelete("/roles/{roleId:guid}", DeleteRoleAsync);
        group.MapPost("/role-assignments", AssignRoleAsync);
        group.MapDelete("/role-assignments/{membershipId:guid}/{roleId:guid}", RemoveRoleAssignmentAsync);
        group.MapGet("/memberships/{membershipId:guid}/effective-permissions", GetEffectivePermissionsAsync);
        group.MapGet("/custom-fields", ListCustomFieldsAsync);
        group.MapPost("/custom-fields", CreateCustomFieldAsync);
        group.MapPut("/custom-fields/{definitionId:guid}", UpdateCustomFieldAsync);
        group.MapDelete("/custom-fields/{definitionId:guid}", DeleteCustomFieldAsync);
        group.MapPut("/custom-fields/{definitionId:guid}/values/{entityId:guid}", UpsertCustomFieldValueAsync);
        group.MapGet("/custom-field-values/{entityType}/{entityId:guid}", GetCustomFieldValuesAsync);
        group.MapPost("/files", BeginFileAsync);
        group.MapPut("/files/{fileId:guid}/content", UploadFileContentAsync).DisableAntiforgery();
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
    private static async Task<IResult> ListPermissionsAsync(ClaimsPrincipal principal, IPermissionService permissions, TenantRoleService roles, CancellationToken ct)
    { var userId = UserId(principal); return await permissions.HasPermissionAsync(userId, Permissions.RolesManage, ct) ? Results.Ok(await roles.ListPermissionsAsync(ct)) : Results.Forbid(); }
    private static async Task<IResult> ListRolesAsync(ClaimsPrincipal principal, IPermissionService permissions, TenantRoleService roles, CancellationToken ct)
    { var userId = UserId(principal); return await permissions.HasPermissionAsync(userId, Permissions.RolesManage, ct) ? Results.Ok(await roles.ListRolesAsync(ct)) : Results.Forbid(); }
    private static async Task<IResult> RenameRoleAsync(Guid roleId, RenameRoleRequest request, ClaimsPrincipal principal, TenantRoleService roles, IAuditWriter audit, CancellationToken ct)
    { var userId = UserId(principal); await roles.RenameRoleAsync(userId, roleId, request.Name, ct); await audit.WriteAsync(userId, "Role.Rename", "TenantRole", roleId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> ReplaceRolePermissionsAsync(Guid roleId, ReplaceRolePermissionsRequest request, ClaimsPrincipal principal, TenantRoleService roles, IAuditWriter audit, CancellationToken ct)
    { var userId = UserId(principal); await roles.ReplacePermissionsAsync(userId, roleId, request.PermissionIds, ct); await audit.WriteAsync(userId, "Role.Permissions.Replace", "TenantRole", roleId.ToString(), "Succeeded", JsonSerializer.Serialize(new { request.PermissionIds }), ct); return Results.NoContent(); }
    private static async Task<IResult> DeleteRoleAsync(Guid roleId, ClaimsPrincipal principal, TenantRoleService roles, IAuditWriter audit, CancellationToken ct)
    { var userId = UserId(principal); await roles.DeleteRoleAsync(userId, roleId, ct); await audit.WriteAsync(userId, "Role.Delete", "TenantRole", roleId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> AssignRoleAsync(AssignRoleRequest request, ClaimsPrincipal principal, TenantRoleService roles, IAuditWriter audit, CancellationToken ct)
    {
        var userId = UserId(principal); await roles.AssignRoleAsync(userId, request.MembershipId, request.RoleId, ct); await audit.WriteAsync(userId, "Role.Assign", "TenantMembership", request.MembershipId.ToString(), "Succeeded", null, ct); return Results.NoContent();
    }
    private static async Task<IResult> RemoveRoleAssignmentAsync(Guid membershipId, Guid roleId, ClaimsPrincipal principal, TenantRoleService roles, IAuditWriter audit, CancellationToken ct)
    { var userId = UserId(principal); await roles.RemoveRoleAssignmentAsync(userId, membershipId, roleId, ct); await audit.WriteAsync(userId, "Role.Unassign", "TenantMembership", membershipId.ToString(), "Succeeded", JsonSerializer.Serialize(new { roleId }), ct); return Results.NoContent(); }
    private static async Task<IResult> GetEffectivePermissionsAsync(Guid membershipId, ClaimsPrincipal principal, TenantRoleService roles, CancellationToken ct)
    { return Results.Ok(await roles.GetEffectivePermissionsAsync(UserId(principal), membershipId, ct)); }

    private static async Task<IResult> ListCustomFieldsAsync(string? entityType, ClaimsPrincipal principal, IPermissionService permissions, ICustomFieldService fields, CancellationToken ct)
    { var userId = UserId(principal); return await permissions.HasPermissionAsync(userId, Permissions.CustomFieldsManage, ct) ? Results.Ok(await fields.ListDefinitionsAsync(entityType, ct)) : Results.Forbid(); }
    private static async Task<IResult> CreateCustomFieldAsync(CustomFieldDefinitionInput request, ClaimsPrincipal principal, ICustomFieldService fields, IAuditWriter audit, CancellationToken ct)
    { var userId = UserId(principal); var id = await fields.CreateDefinitionAsync(userId, request, ct); await audit.WriteAsync(userId, "CustomField.Create", "CustomFieldDefinition", id.ToString(), "Succeeded", null, ct); return Results.Created($"/api/v1/foundation/custom-fields/{id}", new { id }); }
    private static async Task<IResult> UpdateCustomFieldAsync(Guid definitionId, CustomFieldDefinitionInput request, ClaimsPrincipal principal, ICustomFieldService fields, IAuditWriter audit, CancellationToken ct)
    { var userId = UserId(principal); await fields.UpdateDefinitionAsync(userId, definitionId, request, ct); await audit.WriteAsync(userId, "CustomField.Update", "CustomFieldDefinition", definitionId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> DeleteCustomFieldAsync(Guid definitionId, ClaimsPrincipal principal, ICustomFieldService fields, IAuditWriter audit, CancellationToken ct)
    { var userId = UserId(principal); await fields.DeleteDefinitionAsync(userId, definitionId, ct); await audit.WriteAsync(userId, "CustomField.Delete", "CustomFieldDefinition", definitionId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> UpsertCustomFieldValueAsync(Guid definitionId, Guid entityId, CustomFieldValueRequest request, ClaimsPrincipal principal, ICustomFieldService fields, IAuditWriter audit, CancellationToken ct)
    { var userId = UserId(principal); await fields.UpsertValueAsync(userId, definitionId, entityId, request.Value, ct); await audit.WriteAsync(userId, "CustomFieldValue.Upsert", "CustomFieldDefinition", definitionId.ToString(), "Succeeded", JsonSerializer.Serialize(new { entityId }), ct); return Results.NoContent(); }
    private static async Task<IResult> GetCustomFieldValuesAsync(string entityType, Guid entityId, ClaimsPrincipal principal, IPermissionService permissions, ICustomFieldService fields, CancellationToken ct)
    { var userId = UserId(principal); return await permissions.HasPermissionAsync(userId, Permissions.CustomFieldsManage, ct) ? Results.Ok(await fields.GetValuesAsync(entityId, entityType, ct)) : Results.Forbid(); }
    private static async Task<IResult> BeginFileAsync(BeginFileUploadRequest request, ClaimsPrincipal principal, IPermissionService permissions, IFileService files, IAuditWriter audit, CancellationToken ct)
    {
        var userId = UserId(principal); if (!await permissions.HasPermissionAsync(userId, Permissions.FilesManage, ct)) return Results.Forbid();
        var upload = await files.BeginUploadAsync(request.FileName, request.ContentType, request.SizeBytes, request.Category, request.EntityType, request.EntityId, userId, ct); await audit.WriteAsync(userId, "File.BeginUpload", "StoredFile", upload.FileId.ToString(), "Succeeded", null, ct); return Results.Created($"/api/v1/foundation/files/{upload.FileId}", upload);
    }
    private static async Task<IResult> CompleteFileAsync(Guid fileId, CompleteFileUploadRequest request, ClaimsPrincipal principal, IPermissionService permissions, IFileService files, IAuditWriter audit, CancellationToken ct)
    {
        var userId = UserId(principal); if (!await permissions.HasPermissionAsync(userId, Permissions.FilesManage, ct)) return Results.Forbid(); await files.CompleteUploadAsync(fileId, request.Checksum, ct); await audit.WriteAsync(userId, "File.CompleteUpload", "StoredFile", fileId.ToString(), "Succeeded", null, ct); return Results.NoContent();
    }
    private static async Task<IResult> UploadFileContentAsync(Guid fileId, HttpRequest request, ClaimsPrincipal principal, IPermissionService permissions, IFileService files, CancellationToken ct)
    {
        var userId = UserId(principal); if (!await permissions.HasPermissionAsync(userId, Permissions.FilesManage, ct)) return Results.Forbid();
        var contentType = request.ContentType ?? throw new ArgumentException("Content-Type is required.");
        await files.UploadContentAsync(fileId, request.Body, contentType, request.ContentLength, ct);
        return Results.NoContent();
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
