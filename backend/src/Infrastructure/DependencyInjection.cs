using Amazon.S3;
using GiddyEdu.BuildingBlocks.Events;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Caching;
using GiddyEdu.Infrastructure.Health;
using GiddyEdu.Infrastructure.Integrations;
using GiddyEdu.Infrastructure.Jobs;
using GiddyEdu.Infrastructure.Messaging;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Infrastructure.Academics;
using GiddyEdu.Infrastructure.Schools;
using GiddyEdu.Infrastructure.Hr;
using GiddyEdu.Infrastructure.Identity;
using GiddyEdu.Infrastructure.StudentLifecycle;
using GiddyEdu.Infrastructure.Subscriptions;
using GiddyEdu.Modules.Identity.Domain;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using System.Security.Cryptography.X509Certificates;

namespace GiddyEdu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGiddyEduFoundation(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var postgres = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Postgres connection string is required.");
        var redis = configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Redis connection string is required.");
        services.AddScoped<TenantContextAccessor>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContextAccessor>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContextAccessor>());
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IDomainEventDispatcher, InProcessDomainEventDispatcher>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAccessProfileService, AccessProfileService>();
        services.AddScoped<IFeatureAccessGuard, FeatureAccessGuard>();
        services.AddScoped<TenantRoleService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<ICustomFieldValueValidator, CustomFieldValueValidator>();
        services.AddScoped<ICustomFieldTargetRegistry, CustomFieldTargetRegistry>();
        services.AddScoped<ICustomFieldService, CustomFieldService>();
        services.AddScoped<ISchoolAdministrationService, SchoolAdministrationService>();
        services.AddScoped<IAcademicStructureService, AcademicStructureService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IAccountInvitationService, AccountInvitationService>();
        services.AddScoped<IStudentLifecycleService, StudentLifecycleService>();
        services.AddScoped<IAdmissionsCommunicationService, AdmissionsCommunicationService>();
        services.AddScoped<IDataPortabilityService, DataPortabilityService>();
        services.AddScoped<IApplicantImportService, ApplicantImportService>();
        services.AddScoped<ApplicantImportJob>();
        services.AddScoped<IProfileImportService, ProfileImportService>();
        services.AddScoped<ProfileImportJob>();
        services.AddScoped<IImportErrorReportWriter, ImportErrorReportWriter>();
        services.AddSingleton<IObjectKeyFactory, TenantObjectKeyFactory>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<INotificationQueue, NotificationQueue>();
        services.AddScoped<NotificationDeliveryJob>();
        services.AddScoped<NotificationOutboxSweepJob>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddTransient<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<ISubscriptionManagementService, SubscriptionManagementService>();
        services.AddScoped<TenantJobExecutor>();
        services.AddScoped<IIntegrationClient, IntegrationClient>();
        services.AddHttpClient("integrations", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddDbContext<GiddyEduDbContext>(options => options.UseNpgsql(postgres, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public")));
        var dataProtection = services.AddDataProtection().SetApplicationName("GiddyEdu");
        var certificatePath = configuration["DataProtection:CertificatePath"];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, configuration["DataProtection:CertificatePassword"]);
            dataProtection.ProtectKeysWithCertificate(certificate);
        }
        services.AddIdentityCore<PlatformUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.User.RequireUniqueEmail = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<GiddyEduDbContext>().AddDefaultTokenProviders();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
        services.AddScoped<ITenantCache, TenantCache>();
        services.AddHangfire(config => config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(postgres)));
        services.Configure<R2Options>(configuration.GetSection(R2Options.SectionName));
        services.AddSingleton<IAmazonS3>(sp => R2ClientFactory.Create(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<R2Options>>().Value));
        services.AddScoped<IFileObjectStorage, R2FileObjectStorage>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IPhaseOneDocumentService, PhaseOneDocumentService>();
        var checks = services.AddHealthChecks().AddDbContextCheck<GiddyEduDbContext>("postgres", tags: ["ready"]).AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
        if (!string.IsNullOrWhiteSpace(configuration[$"{R2Options.SectionName}:Endpoint"])) checks.AddCheck<R2HealthCheck>("r2", tags: ["ready"]);
        if (!string.IsNullOrWhiteSpace(configuration[$"{SmtpOptions.SectionName}:Host"])) checks.AddCheck<SmtpHealthCheck>("smtp", tags: ["ready"]);
        var useOtlp = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        services.AddOpenTelemetry().ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t => { t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation(); if (useOtlp) t.AddOtlpExporter(); })
            .WithMetrics(m => { m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation(); if (useOtlp) m.AddOtlpExporter(); });
        return services;
    }
}
