using System.Text.Json;
using System.Text.RegularExpressions;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Platform;

public sealed record CustomFieldOptionInput(string Value, string Label, int DisplayOrder);
public sealed record CustomFieldDefinitionInput(string TargetModule, string TargetEntityType, string FieldKey, string Label, string? HelpText,
    CustomFieldDataType DataType, bool IsRequired, bool IsActive, int DisplayOrder, JsonElement? DefaultValue, JsonElement? Validation, IReadOnlyCollection<CustomFieldOptionInput> Options);
public sealed record CustomFieldOptionInfo(Guid Id, string Value, string Label, int DisplayOrder);
public sealed record CustomFieldDefinitionInfo(Guid Id, string TargetModule, string TargetEntityType, string FieldKey, string Label, string? HelpText,
    CustomFieldDataType DataType, bool IsRequired, bool IsActive, int DisplayOrder, string? DefaultValue, string? ValidationJson, IReadOnlyCollection<CustomFieldOptionInfo> Options);
public sealed record CustomFieldValueInfo(Guid DefinitionId, string EntityType, Guid EntityId, string ValueJson, DateTimeOffset UpdatedAtUtc);

public interface ICustomFieldTargetRegistry
{
    bool Supports(string module, string entityType);
    Task<bool> ExistsInCurrentTenantAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
}

public interface ICustomFieldService
{
    Task<IReadOnlyCollection<CustomFieldDefinitionInfo>> ListDefinitionsAsync(string? entityType, CancellationToken cancellationToken = default);
    Task<Guid> CreateDefinitionAsync(Guid actorUserId, CustomFieldDefinitionInput input, CancellationToken cancellationToken = default);
    Task UpdateDefinitionAsync(Guid actorUserId, Guid definitionId, CustomFieldDefinitionInput input, CancellationToken cancellationToken = default);
    Task DeleteDefinitionAsync(Guid actorUserId, Guid definitionId, CancellationToken cancellationToken = default);
    Task UpsertValueAsync(Guid actorUserId, Guid definitionId, Guid entityId, JsonElement value, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CustomFieldValueInfo>> GetValuesAsync(Guid entityId, string entityType, CancellationToken cancellationToken = default);
}

public sealed partial class CustomFieldTargetRegistry(GiddyEduDbContext db, ITenantContext tenant) : ICustomFieldTargetRegistry
{
    private static readonly HashSet<(string Module, string Entity)> Supported = new(StringTupleComparer.OrdinalIgnoreCase)
    { ("Tenancy", "Tenant"), ("Tenancy", "Campus"), ("Tenancy", "TenantMembership") };

    public bool Supports(string module, string entityType) => Supported.Contains((module.Trim(), entityType.Trim()));

    public Task<bool> ExistsInCurrentTenantAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        return entityType.Trim().ToLowerInvariant() switch
        {
            "tenant" => Task.FromResult(entityId == tenantId),
            "campus" => db.Campuses.AnyAsync(x => x.Id == entityId && x.IsActive, ct),
            "tenantmembership" => db.TenantMemberships.AnyAsync(x => x.Id == entityId && x.IsActive, ct),
            _ => Task.FromResult(false)
        };
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Module, string Entity)>
    {
        public static readonly StringTupleComparer OrdinalIgnoreCase = new();
        public bool Equals((string Module, string Entity) x, (string Module, string Entity) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Module, y.Module) && StringComparer.OrdinalIgnoreCase.Equals(x.Entity, y.Entity);
        public int GetHashCode((string Module, string Entity) value) => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.Module), StringComparer.OrdinalIgnoreCase.GetHashCode(value.Entity));
    }
}

public sealed partial class CustomFieldService(GiddyEduDbContext db, ITenantContext tenant, IPermissionService permissions,
    ICustomFieldValueValidator validator, ICustomFieldTargetRegistry targets, IClock clock)
    : ICustomFieldService
{
    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    { "id", "tenantid", "ownerid", "userid", "password", "passwordhash", "authenticationdata", "permission", "permissionid", "permissions", "role", "roleid", "roles", "audit", "auditmetadata", "createdat", "updatedat", "paymentstatus", "ledgerstate", "result", "calculatedresult", "grade" };

    public async Task<IReadOnlyCollection<CustomFieldDefinitionInfo>> ListDefinitionsAsync(string? entityType, CancellationToken ct = default)
    {
        RequireTenant();
        var query = db.CustomFieldDefinitions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(x => x.TargetEntityType == entityType.Trim());
        var definitions = await query.OrderBy(x => x.TargetEntityType).ThenBy(x => x.DisplayOrder).ThenBy(x => x.FieldKey).ToListAsync(ct);
        return await MapAsync(definitions, ct);
    }

    public async Task<Guid> CreateDefinitionAsync(Guid actorUserId, CustomFieldDefinitionInput input, CancellationToken ct = default)
    {
        await DemandManageAsync(actorUserId, ct); ValidateDefinitionInput(input);
        var tenantId = RequireTenant(); var id = Guid.NewGuid();
        var definition = new CustomFieldDefinition(id, tenantId, input.TargetModule, input.TargetEntityType, input.FieldKey, input.Label, input.DataType, clock.UtcNow);
        definition.Update(input.Label, input.HelpText, input.IsRequired, input.IsActive, input.DisplayOrder, Raw(input.DefaultValue), Raw(input.Validation), clock.UtcNow);
        ValidateDefault(definition); db.CustomFieldDefinitions.Add(definition); AddOptions(tenantId, id, input);
        await db.SaveChangesAsync(ct); return id;
    }

    public async Task UpdateDefinitionAsync(Guid actorUserId, Guid definitionId, CustomFieldDefinitionInput input, CancellationToken ct = default)
    {
        await DemandManageAsync(actorUserId, ct); ValidateDefinitionInput(input);
        var definition = await db.CustomFieldDefinitions.SingleOrDefaultAsync(x => x.Id == definitionId, ct) ?? throw new KeyNotFoundException("Custom-field definition was not found.");
        if (!definition.TargetModule.Equals(input.TargetModule.Trim(), StringComparison.OrdinalIgnoreCase) || !definition.TargetEntityType.Equals(input.TargetEntityType.Trim(), StringComparison.OrdinalIgnoreCase)
            || !definition.FieldKey.Equals(input.FieldKey.Trim(), StringComparison.OrdinalIgnoreCase) || definition.DataType != input.DataType)
            throw new InvalidOperationException("A custom field's target, key, and data type are immutable; create a new definition instead.");
        definition.Update(input.Label, input.HelpText, input.IsRequired, input.IsActive, input.DisplayOrder, Raw(input.DefaultValue), Raw(input.Validation), clock.UtcNow);
        ValidateDefault(definition);
        var existingOptions = await db.CustomFieldOptions.Where(x => x.DefinitionId == definitionId).ToListAsync(ct); db.CustomFieldOptions.RemoveRange(existingOptions);
        AddOptions(RequireTenant(), definitionId, input); await db.SaveChangesAsync(ct);
    }

    public async Task DeleteDefinitionAsync(Guid actorUserId, Guid definitionId, CancellationToken ct = default)
    {
        await DemandManageAsync(actorUserId, ct);
        var definition = await db.CustomFieldDefinitions.SingleOrDefaultAsync(x => x.Id == definitionId, ct) ?? throw new KeyNotFoundException("Custom-field definition was not found.");
        if (await db.CustomFieldValues.AnyAsync(x => x.DefinitionId == definitionId, ct)) throw new InvalidOperationException("Definitions with stored values must be deactivated instead of deleted.");
        db.CustomFieldDefinitions.Remove(definition); await db.SaveChangesAsync(ct);
    }

    public async Task UpsertValueAsync(Guid actorUserId, Guid definitionId, Guid entityId, JsonElement value, CancellationToken ct = default)
    {
        await DemandManageAsync(actorUserId, ct);
        var definition = await db.CustomFieldDefinitions.SingleOrDefaultAsync(x => x.Id == definitionId && x.IsActive, ct) ?? throw new KeyNotFoundException("Active custom-field definition was not found.");
        if (!await targets.ExistsInCurrentTenantAsync(definition.TargetEntityType, entityId, ct)) throw new InvalidOperationException("The target entity is unsupported or does not belong to the current tenant.");
        var raw = value.GetRawText(); if (!validator.IsValid(definition, raw)) throw new ArgumentException("The custom-field value does not satisfy its type or validation rules.", nameof(value));
        await ValidateSelectionAsync(definition, value, ct);
        var existing = await db.CustomFieldValues.SingleOrDefaultAsync(x => x.DefinitionId == definitionId && x.EntityId == entityId, ct);
        if (existing is null) db.CustomFieldValues.Add(new CustomFieldValue(Guid.NewGuid(), RequireTenant(), definitionId, definition.TargetEntityType, entityId, raw, clock.UtcNow)); else existing.Update(raw, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<CustomFieldValueInfo>> GetValuesAsync(Guid entityId, string entityType, CancellationToken ct = default)
    {
        RequireTenant();
        if (!targets.Supports("Tenancy", entityType) || !await targets.ExistsInCurrentTenantAsync(entityType, entityId, ct)) throw new KeyNotFoundException("Extensible target was not found.");
        return await (from value in db.CustomFieldValues join definition in db.CustomFieldDefinitions on value.DefinitionId equals definition.Id
                      where value.EntityId == entityId && value.EntityType == entityType && definition.IsActive
                      orderby definition.DisplayOrder, definition.FieldKey
                      select new CustomFieldValueInfo(value.DefinitionId, value.EntityType, value.EntityId, value.ValueJson, value.UpdatedAtUtc)).AsNoTracking().ToListAsync(ct);
    }

    private async Task ValidateSelectionAsync(CustomFieldDefinition definition, JsonElement value, CancellationToken ct)
    {
        if (definition.DataType is not (CustomFieldDataType.SingleSelect or CustomFieldDataType.MultiSelect)) return;
        var allowed = (await db.CustomFieldOptions.Where(x => x.DefinitionId == definition.Id).Select(x => x.Value).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var submitted = definition.DataType == CustomFieldDataType.SingleSelect ? [value.GetString()!] : value.EnumerateArray().Select(x => x.GetString()!).ToArray();
        if (submitted.Any(x => !allowed.Contains(x))) throw new ArgumentException("A selection is not one of the configured options.", nameof(value));
    }

    private void ValidateDefinitionInput(CustomFieldDefinitionInput input)
    {
        if (!targets.Supports(input.TargetModule, input.TargetEntityType)) throw new ArgumentException("The requested module/entity is not registered for custom fields.", nameof(input));
        var key = input.FieldKey.Trim(); var normalizedKey = string.Concat(key.Where(char.IsLetterOrDigit));
        if (!FieldKeyPattern().IsMatch(key) || ReservedKeys.Contains(normalizedKey)) throw new ArgumentException("FieldKey is invalid or reserved for a platform concept.", nameof(input));
        if (input.Options.GroupBy(x => x.Value, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new ArgumentException("Option values must be unique.", nameof(input));
        var isSelection = input.DataType is CustomFieldDataType.SingleSelect or CustomFieldDataType.MultiSelect;
        if (isSelection != (input.Options.Count > 0)) throw new ArgumentException("Selection fields require options and non-selection fields cannot define options.", nameof(input));
        if (input.Validation.HasValue && input.Validation.Value.ValueKind != JsonValueKind.Object) throw new ArgumentException("Validation must be a JSON object.", nameof(input));
        if (input.DefaultValue.HasValue && isSelection)
        {
            var allowed = input.Options.Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
            var defaults = input.DataType == CustomFieldDataType.SingleSelect
                ? input.DefaultValue.Value.ValueKind == JsonValueKind.String ? [input.DefaultValue.Value.GetString()!] : []
                : input.DefaultValue.Value.ValueKind == JsonValueKind.Array ? input.DefaultValue.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : [];
            if (!defaults.Any() || defaults.Any(x => !allowed.Contains(x))) throw new ArgumentException("DefaultValue must use configured selection options.", nameof(input));
        }
    }

    private void ValidateDefault(CustomFieldDefinition definition) { if (definition.DefaultValue is not null && !validator.IsValid(definition, definition.DefaultValue)) throw new ArgumentException("DefaultValue does not satisfy the field rules."); }
    private void AddOptions(Guid tenantId, Guid definitionId, CustomFieldDefinitionInput input) => db.CustomFieldOptions.AddRange(input.Options.Select(x => new CustomFieldOption(Guid.NewGuid(), tenantId, definitionId, x.Value, x.Label, x.DisplayOrder)));
    private async Task DemandManageAsync(Guid actor, CancellationToken ct) { if (!await permissions.HasPermissionAsync(actor, Permissions.CustomFieldsManage, ct)) throw new UnauthorizedAccessException("CustomFields.Manage is required."); }
    private Guid RequireTenant() => tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    private static string? Raw(JsonElement? value) => value.HasValue ? value.Value.GetRawText() : null;
    private async Task<IReadOnlyCollection<CustomFieldDefinitionInfo>> MapAsync(IReadOnlyCollection<CustomFieldDefinition> definitions, CancellationToken ct)
    {
        var ids = definitions.Select(x => x.Id).ToArray(); var options = await db.CustomFieldOptions.AsNoTracking().Where(x => ids.Contains(x.DefinitionId)).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        return definitions.Select(x => new CustomFieldDefinitionInfo(x.Id, x.TargetModule, x.TargetEntityType, x.FieldKey, x.Label, x.HelpText, x.DataType, x.IsRequired, x.IsActive,
            x.DisplayOrder, x.DefaultValue, x.ValidationJson, options.Where(o => o.DefinitionId == x.Id).Select(o => new CustomFieldOptionInfo(o.Id, o.Value, o.Label, o.DisplayOrder)).ToArray())).ToArray();
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldKeyPattern();
}
