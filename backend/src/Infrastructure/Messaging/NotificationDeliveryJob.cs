using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Hangfire;

namespace GiddyEdu.Infrastructure.Messaging;

public sealed record EmailNotificationPayload(string Subject, string HtmlBody, string? TextBody);

public sealed class NotificationDeliveryJob(GiddyEduDbContext db, ITenantContextSetter tenant, IEmailSender email, IClock clock)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task DeliverAsync(Guid tenantId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        tenant.Set(tenantId, null);
        try
        {
            var message = await db.NotificationMessages.SingleOrDefaultAsync(x => x.Id == notificationId, cancellationToken) ?? throw new KeyNotFoundException("Notification not found.");
            if (message.Status == NotificationStatus.Sent) return;
            message.MarkProcessing(); await db.SaveChangesAsync(cancellationToken);
            try
            {
                if (!string.Equals(message.Channel, "email", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException($"Notification channel '{message.Channel}' is not configured.");
                var payload = JsonSerializer.Deserialize<EmailNotificationPayload>(message.PayloadJson) ?? throw new InvalidOperationException("Email payload is invalid.");
                await email.SendAsync(message.Recipient, payload.Subject, payload.HtmlBody, payload.TextBody, cancellationToken);
                message.MarkSent(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                message.MarkFailed(exception.Message, clock.UtcNow); await db.SaveChangesAsync(cancellationToken); throw;
            }
        }
        finally { tenant.Clear(); }
    }
}

public sealed class NotificationOutboxSweepJob(GiddyEduDbContext db, IBackgroundJobClient jobs)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db.NotificationMessages.IgnoreQueryFilters()
            .Where(x => (x.Status == NotificationStatus.Pending || x.Status == NotificationStatus.Failed) && x.AttemptCount < 5)
            .OrderBy(x => x.CreatedAtUtc).Take(100).Select(x => new { x.TenantId, x.Id }).ToListAsync(cancellationToken);
        foreach (var message in pending) jobs.Enqueue<NotificationDeliveryJob>(job => job.DeliverAsync(message.TenantId, message.Id, CancellationToken.None));
    }
}
