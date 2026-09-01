using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Platform.Domain;

public enum CustomFieldDataType { ShortText, LongText, Integer, Decimal, Date, DateTime, Boolean, Email, Phone, Url, SingleSelect, MultiSelect }

public sealed class CustomFieldDefinition : ITenantOwned
{
    private CustomFieldDefinition() { }
    public CustomFieldDefinition(Guid id, Guid tenantId, string targetModule, string targetEntityType, string fieldKey, string label, CustomFieldDataType dataType, DateTimeOffset createdAtUtc)
    {
        Id = id; TenantId = tenantId; TargetModule = targetModule.Trim(); TargetEntityType = targetEntityType.Trim();
        FieldKey = fieldKey.Trim().ToLowerInvariant(); Label = label.Trim(); DataType = dataType; CreatedAtUtc = createdAtUtc; IsActive = true;
    }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string TargetModule { get; private set; } = null!;
    public string TargetEntityType { get; private set; } = null!;
    public string FieldKey { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public string? HelpText { get; private set; }
    public CustomFieldDataType DataType { get; private set; }
    public bool IsRequired { get; private set; }
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public string? DefaultValue { get; private set; }
    public string? ValidationJson { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public void Update(string label, string? helpText, bool isRequired, bool isActive, int displayOrder, string? defaultValue, string? validationJson, DateTimeOffset updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > 200) throw new ArgumentException("A label of at most 200 characters is required.", nameof(label));
        Label = label.Trim(); HelpText = string.IsNullOrWhiteSpace(helpText) ? null : helpText.Trim(); IsRequired = isRequired; IsActive = isActive;
        DisplayOrder = displayOrder; DefaultValue = defaultValue; ValidationJson = validationJson; UpdatedAtUtc = updatedAtUtc;
    }
}

public sealed class CustomFieldOption : ITenantOwned
{
    private CustomFieldOption() { }
    public CustomFieldOption(Guid id, Guid tenantId, Guid definitionId, string value, string label, int displayOrder)
    { Id = id; TenantId = tenantId; DefinitionId = definitionId; Value = value.Trim(); Label = label.Trim(); DisplayOrder = displayOrder; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid DefinitionId { get; private set; }
    public string Value { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public int DisplayOrder { get; private set; }
}

public sealed class CustomFieldValue : ITenantOwned
{
    private CustomFieldValue() { }
    public CustomFieldValue(Guid id, Guid tenantId, Guid definitionId, string entityType, Guid entityId, string valueJson, DateTimeOffset updatedAtUtc)
    { Id = id; TenantId = tenantId; DefinitionId = definitionId; EntityType = entityType.Trim(); EntityId = entityId; ValueJson = valueJson; UpdatedAtUtc = updatedAtUtc; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid DefinitionId { get; private set; }
    public string EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }
    public string ValueJson { get; private set; } = null!;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(string valueJson, DateTimeOffset updatedAtUtc) { ValueJson = valueJson; UpdatedAtUtc = updatedAtUtc; }
}
