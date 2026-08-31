using System.Security.Claims;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Infrastructure.Messaging;
using Hangfire;

namespace GiddyEdu.Infrastructure.Platform;

public interface IAuditWriter
{
    Task WriteAsync(Guid? actorUserId, string action, string targetType, string targetId, string result, string? metadataJson, CancellationToken cancellationToken = default);
}

public sealed class AuditWriter(GiddyEduDbContext db, ITenantContext tenant, IClock clock) : IAuditWriter
{
    public async Task WriteAsync(Guid? actorUserId, string action, string targetType, string targetId, string result, string? metadataJson, CancellationToken cancellationToken = default)
    {
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required for audit records.");
        db.AuditRecords.Add(new AuditRecord(Guid.NewGuid(), tenantId, actorUserId, action, targetType, targetId, result, clock.UtcNow, metadataJson));
        await db.SaveChangesAsync(cancellationToken);
    }
}

public interface INotificationQueue
{
    Task<Guid> EnqueueAsync(string channel, string recipient, string templateKey, string payloadJson, CancellationToken cancellationToken = default);
}

public sealed class NotificationQueue(GiddyEduDbContext db, ITenantContext tenant, IClock clock, IBackgroundJobClient jobs) : INotificationQueue
{
    public async Task<Guid> EnqueueAsync(string channel, string recipient, string templateKey, string payloadJson, CancellationToken cancellationToken = default)
    {
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required for notifications.");
        var id = Guid.NewGuid();
        db.NotificationMessages.Add(new NotificationMessage(id, tenantId, channel, recipient, templateKey, payloadJson, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        jobs.Enqueue<NotificationDeliveryJob>(job => job.DeliverAsync(tenantId, id, CancellationToken.None));
        return id;
    }
}
