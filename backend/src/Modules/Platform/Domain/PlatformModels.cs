using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Platform.Domain;

public sealed class TenantSetting : ITenantOwned
{
    private TenantSetting() { }
    public TenantSetting(Guid tenantId, string key, string valueJson, DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    { TenantId = tenantId; Key = key.Trim(); ValueJson = valueJson; UpdatedAtUtc = updatedAtUtc; UpdatedByUserId = updatedByUserId; }
    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = null!;
    public string ValueJson { get; private set; } = null!;
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public void Update(string valueJson, DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    { ValueJson = valueJson; UpdatedAtUtc = updatedAtUtc; UpdatedByUserId = updatedByUserId; }
}

public sealed class CampusSetting : ITenantOwned
{
    private CampusSetting() { }
    public CampusSetting(Guid tenantId, Guid campusId, string key, string valueJson, DateTimeOffset updatedAtUtc)
    { TenantId = tenantId; CampusId = campusId; Key = key.Trim(); ValueJson = valueJson; UpdatedAtUtc = updatedAtUtc; }
    public Guid TenantId { get; private set; }
    public Guid CampusId { get; private set; }
    public string Key { get; private set; } = null!;
    public string ValueJson { get; private set; } = null!;
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void Update(string valueJson, DateTimeOffset updatedAtUtc) { ValueJson = valueJson; UpdatedAtUtc = updatedAtUtc; }
}

public sealed class FeatureFlag
{
    private FeatureFlag() { }
    public FeatureFlag(string key, bool enabled, string environment) { Key = key.Trim(); Enabled = enabled; Environment = environment.Trim(); }
    public string Key { get; private set; } = null!;
    public bool Enabled { get; private set; }
    public string Environment { get; private set; } = null!;
}

public sealed class AuditRecord : ITenantOwned
{
    private AuditRecord() { }
    public AuditRecord(Guid id, Guid tenantId, Guid? actorUserId, string action, string targetType, string targetId, string result, DateTimeOffset occurredAtUtc, string? metadataJson)
    { Id = id; TenantId = tenantId; ActorUserId = actorUserId; Action = action; TargetType = targetType; TargetId = targetId; Result = result; OccurredAtUtc = occurredAtUtc; MetadataJson = metadataJson; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = null!;
    public string TargetType { get; private set; } = null!;
    public string TargetId { get; private set; } = null!;
    public string Result { get; private set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string? MetadataJson { get; private set; }
}

public enum StoredFileStatus { PendingUpload, Available, Deleted }

public sealed class StoredFile : ITenantOwned
{
    private StoredFile() { }
    public StoredFile(Guid id, Guid tenantId, string objectKey, string originalFileName, string contentType, long sizeBytes, string category, string entityType, Guid entityId, Guid uploadedByUserId, DateTimeOffset createdAtUtc)
    { Id = id; TenantId = tenantId; ObjectKey = objectKey; OriginalFileName = originalFileName; ContentType = contentType; SizeBytes = sizeBytes; Category = category; EntityType = entityType; EntityId = entityId; UploadedByUserId = uploadedByUserId; CreatedAtUtc = createdAtUtc; Status = StoredFileStatus.PendingUpload; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ObjectKey { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string? Checksum { get; private set; }
    public string Category { get; private set; } = null!;
    public string EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public StoredFileStatus Status { get; private set; }
    public void MarkAvailable(string checksum)
    {
        if (Status != StoredFileStatus.PendingUpload) throw new InvalidOperationException("Only pending uploads can become available.");
        var normalized = checksum?.Trim().ToUpperInvariant();
        if (normalized is null || normalized.Length != 64 || normalized.Any(c => !char.IsAsciiHexDigit(c))) throw new ArgumentException("A SHA-256 checksum is required.", nameof(checksum));
        Checksum = normalized;
        Status = StoredFileStatus.Available;
    }
    public void MarkDeleted() => Status = StoredFileStatus.Deleted;
}

public enum NotificationStatus { Pending, Processing, Sent, Failed }

public sealed class NotificationMessage : ITenantOwned
{
    private NotificationMessage() { }
    public NotificationMessage(Guid id, Guid tenantId, string channel, string recipient, string templateKey, string payloadJson, DateTimeOffset createdAtUtc)
    { Id = id; TenantId = tenantId; Channel = channel; Recipient = recipient; TemplateKey = templateKey; PayloadJson = payloadJson; CreatedAtUtc = createdAtUtc; Status = NotificationStatus.Pending; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Channel { get; private set; } = null!;
    public string Recipient { get; private set; } = null!;
    public string TemplateKey { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public NotificationStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    public void MarkProcessing()
    {
        if (Status is NotificationStatus.Sent) throw new InvalidOperationException("A sent notification cannot be processed again.");
        Status = NotificationStatus.Processing;
        AttemptCount++;
    }
    public void MarkSent(DateTimeOffset processedAtUtc) { Status = NotificationStatus.Sent; ProcessedAtUtc = processedAtUtc; LastError = null; }
    public void MarkFailed(string error, DateTimeOffset processedAtUtc)
    {
        Status = NotificationStatus.Failed;
        ProcessedAtUtc = processedAtUtc;
        LastError = error.Length <= 500 ? error : error[..500];
    }
}
