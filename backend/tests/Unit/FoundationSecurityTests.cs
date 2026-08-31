using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using GiddyEdu.Infrastructure.Messaging;
using GiddyEdu.BuildingBlocks.Time;

namespace GiddyEdu.UnitTests;

public sealed class FoundationSecurityTests
{
    [Fact]
    public void ObjectKeys_AreTenantScopedAndServerGenerated()
    {
        var tenantId = Guid.NewGuid();
        var key = new TenantObjectKeyFactory().Create(tenantId, "students", "documents", Guid.NewGuid(), Guid.NewGuid(), "pdf");
        Assert.StartsWith($"tenants/{tenantId:D}/students/documents/", key, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new TenantObjectKeyFactory().Create(tenantId, "../escape", "documents", Guid.NewGuid(), Guid.NewGuid(), "pdf"));
    }

    [Theory]
    [InlineData(CustomFieldDataType.Integer, "123", true)]
    [InlineData(CustomFieldDataType.Integer, "\"123\"", false)]
    [InlineData(CustomFieldDataType.Email, "\"parent@example.com\"", true)]
    [InlineData(CustomFieldDataType.Email, "\"invalid\"", false)]
    [InlineData(CustomFieldDataType.MultiSelect, "[\"a\",\"b\"]", true)]
    public void CustomFieldValues_AreValidatedByDeclaredType(CustomFieldDataType type, string json, bool expected)
    {
        var definition = new CustomFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), "Platform", "Tenant", "field", "Field", type, DateTimeOffset.UtcNow);
        Assert.Equal(expected, new CustomFieldValueValidator().IsValid(definition, json));
    }

    [Fact]
    public void RefreshTokens_AreSingleUseAndExpire()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, new string('A', 64), now, now.AddDays(1));
        Assert.True(token.IsUsable(now));
        token.Revoke(now);
        Assert.False(token.IsUsable(now));
        Assert.False(new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, new string('B', 64), now.AddDays(-2), now.AddDays(-1)).IsUsable(now));
    }

    [Fact]
    public void NotificationState_PreventsResendingAndTracksFailure()
    {
        var now = DateTimeOffset.UtcNow;
        var failed = new NotificationMessage(Guid.NewGuid(), Guid.NewGuid(), "email", "a@example.com", "test", "{}", now);
        failed.MarkProcessing(); failed.MarkFailed("provider failure", now);
        Assert.Equal(NotificationStatus.Failed, failed.Status); Assert.Equal(1, failed.AttemptCount);
        var sent = new NotificationMessage(Guid.NewGuid(), Guid.NewGuid(), "email", "a@example.com", "test", "{}", now);
        sent.MarkProcessing(); sent.MarkSent(now);
        Assert.Throws<InvalidOperationException>(() => sent.MarkProcessing());
    }

    [Fact]
    public async Task RoleManagement_RejectsActorWithoutManagementPermission()
    {
        var tenant = new TenantContextAccessor(); tenant.Set(Guid.NewGuid(), null);
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, tenant);
        var service = new TenantRoleService(db, tenant, new DeniedPermissionService());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateRoleAsync(Guid.NewGuid(), "Administrator", []));
    }

    private sealed class DeniedPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    [Fact]
    public async Task NotificationDelivery_SendsAndMarksMessageSent()
    {
        var tenantId = Guid.NewGuid(); var notificationId = Guid.NewGuid(); var tenant = new TenantContextAccessor(); tenant.Set(tenantId, null);
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, tenant);
        db.NotificationMessages.Add(new NotificationMessage(notificationId, tenantId, "email", "recipient@example.com", "test", "{\"subject\":\"Test\",\"htmlBody\":\"<p>Test</p>\",\"textBody\":\"Test\"}", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(); tenant.Clear();
        var sender = new RecordingEmailSender();
        await new NotificationDeliveryJob(db, tenant, sender, new SystemClock()).DeliverAsync(tenantId, notificationId);
        tenant.Set(tenantId, null);
        Assert.True(sender.Sent); Assert.Equal(NotificationStatus.Sent, (await db.NotificationMessages.SingleAsync()).Status);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public bool Sent { get; private set; }
        public Task SendAsync(string recipient, string subject, string htmlBody, string? textBody = null, CancellationToken cancellationToken = default) { Sent = true; return Task.CompletedTask; }
    }
}
