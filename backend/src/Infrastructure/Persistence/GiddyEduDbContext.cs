using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.Subscriptions.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Persistence;

public sealed class GiddyEduDbContext(
    DbContextOptions<GiddyEduDbContext> options,
    ITenantContext tenantContext)
    : IdentityDbContext<PlatformUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<TenantRole> TenantRoles => Set<TenantRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<TenantMembershipRole> TenantMembershipRoles => Set<TenantMembershipRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<TenantEntitlementOverride> TenantEntitlementOverrides => Set<TenantEntitlementOverride>();
    public DbSet<CampusEntitlementOverride> CampusEntitlementOverrides => Set<CampusEntitlementOverride>();
    public DbSet<AddOn> AddOns => Set<AddOn>();
    public DbSet<TenantAddOn> TenantAddOns => Set<TenantAddOn>();
    public DbSet<FeatureUsage> FeatureUsage => Set<FeatureUsage>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldOption> CustomFieldOptions => Set<CustomFieldOption>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<CampusSetting> CampusSettings => Set<CampusSetting>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("giddyedu");
        ConfigureTenancy(modelBuilder);
        ConfigureIdentity(modelBuilder);
        ConfigureSubscriptions(modelBuilder);
        ConfigurePlatform(modelBuilder);
        ApplyTenantFilters(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ValidateChanges()
    {
        if (ChangeTracker.Entries<AuditRecord>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit records are append-only.");
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (!tenantContext.TenantId.HasValue || entry.Entity.TenantId != tenantContext.TenantId.Value)
                throw new InvalidOperationException("A tenant-owned record cannot be changed outside its trusted tenant context.");
        }
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Campus>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TenantMembership>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TenantRole>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<RolePermission>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TenantMembershipRole>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TenantSubscription>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TenantEntitlementOverride>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<CampusEntitlementOverride>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TenantAddOn>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<FeatureUsage>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<CustomFieldDefinition>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<CustomFieldOption>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<CustomFieldValue>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TenantSetting>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<CampusSetting>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<AuditRecord>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<StoredFile>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<NotificationMessage>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
    }

    private static void ConfigureTenancy(ModelBuilder b)
    {
        b.Entity<Tenant>(e => { e.ToTable("Tenants"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(200); e.Property(x => x.Slug).HasMaxLength(100); e.HasIndex(x => x.Slug).IsUnique(); });
        b.Entity<Campus>(e => { e.ToTable("Campuses"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(200); e.Property(x => x.Code).HasMaxLength(50); e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TenantMembership>(e => { e.ToTable("TenantMemberships"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique(); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); e.HasOne<PlatformUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
    }

    private static void ConfigureIdentity(ModelBuilder b)
    {
        b.Entity<Permission>(e => { e.ToTable("Permissions"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(150); e.HasIndex(x => x.Name).IsUnique(); });
        b.Entity<TenantRole>(e => { e.ToTable("TenantRoles"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(100); e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique(); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<RolePermission>(e => { e.ToTable("RolePermissions"); e.HasKey(x => new { x.TenantId, x.RoleId, x.PermissionId }); e.HasOne<TenantRole>().WithMany().HasForeignKey(x => new { x.TenantId, x.RoleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); e.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TenantMembership>(e => e.HasAlternateKey(x => new { x.TenantId, x.Id }));
        b.Entity<TenantMembershipRole>(e => { e.ToTable("TenantMembershipRoles"); e.HasKey(x => new { x.TenantId, x.MembershipId, x.RoleId }); e.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.MembershipId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); e.HasOne<TenantRole>().WithMany().HasForeignKey(x => new { x.TenantId, x.RoleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<RefreshToken>(e => { e.ToTable("RefreshTokens"); e.HasKey(x => x.Id); e.HasIndex(x => x.TokenHash).IsUnique(); e.HasIndex(x => new { x.TenantId, x.UserId, x.ExpiresAtUtc }); e.Property(x => x.TokenHash).HasMaxLength(64); });
    }

    private static void ConfigureSubscriptions(ModelBuilder b)
    {
        b.Entity<Plan>(e => { e.ToTable("Plans"); e.HasKey(x => x.Id); e.HasIndex(x => x.Code).IsUnique(); });
        b.Entity<Feature>(e => { e.ToTable("Features"); e.HasKey(x => x.Id); e.HasIndex(x => x.Key).IsUnique(); });
        b.Entity<PlanEntitlement>(e => { e.ToTable("PlanEntitlements"); e.HasKey(x => new { x.PlanId, x.FeatureId }); });
        b.Entity<TenantSubscription>(e => { e.ToTable("TenantSubscriptions"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.IsActive }); });
        b.Entity<TenantEntitlementOverride>(e => { e.ToTable("TenantEntitlementOverrides"); e.HasKey(x => new { x.TenantId, x.FeatureId }); });
        b.Entity<CampusEntitlementOverride>(e => { e.ToTable("CampusEntitlementOverrides"); e.HasKey(x => new { x.TenantId, x.CampusId, x.FeatureId }); });
        b.Entity<AddOn>(e => { e.ToTable("AddOns"); e.HasKey(x => x.Id); e.HasIndex(x => x.Code).IsUnique(); });
        b.Entity<TenantAddOn>(e => { e.ToTable("TenantAddOns"); e.HasKey(x => new { x.TenantId, x.AddOnId }); });
        b.Entity<FeatureUsage>(e => { e.ToTable("FeatureUsage"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.FeatureId, x.PeriodStart }).IsUnique(); });
    }

    private static void ConfigurePlatform(ModelBuilder b)
    {
        b.Entity<CustomFieldDefinition>(e => { e.ToTable("CustomFieldDefinitions"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.HasIndex(x => new { x.TenantId, x.TargetEntityType, x.FieldKey }).IsUnique(); e.Property(x => x.ValidationJson).HasColumnType("jsonb"); });
        b.Entity<CustomFieldOption>(e => { e.ToTable("CustomFieldOptions"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.DefinitionId, x.Value }).IsUnique(); e.HasOne<CustomFieldDefinition>().WithMany().HasForeignKey(x => new { x.TenantId, x.DefinitionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<CustomFieldValue>(e => { e.ToTable("CustomFieldValues"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.DefinitionId, x.EntityId }).IsUnique(); e.HasOne<CustomFieldDefinition>().WithMany().HasForeignKey(x => new { x.TenantId, x.DefinitionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); e.Property(x => x.ValueJson).HasColumnType("jsonb"); });
        b.Entity<TenantSetting>(e => { e.ToTable("TenantSettings"); e.HasKey(x => new { x.TenantId, x.Key }); e.Property(x => x.ValueJson).HasColumnType("jsonb"); });
        b.Entity<CampusSetting>(e => { e.ToTable("CampusSettings"); e.HasKey(x => new { x.TenantId, x.CampusId, x.Key }); e.Property(x => x.ValueJson).HasColumnType("jsonb"); });
        b.Entity<FeatureFlag>(e => { e.ToTable("FeatureFlags"); e.HasKey(x => new { x.Key, x.Environment }); });
        b.Entity<AuditRecord>(e => { e.ToTable("AuditRecords"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.OccurredAtUtc }); e.Property(x => x.MetadataJson).HasColumnType("jsonb"); });
        b.Entity<StoredFile>(e => { e.ToTable("StoredFiles"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.ObjectKey }).IsUnique(); e.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId }); });
        b.Entity<NotificationMessage>(e => { e.ToTable("NotificationMessages"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAtUtc }); e.Property(x => x.PayloadJson).HasColumnType("jsonb"); e.Property(x => x.LastError).HasMaxLength(500); });
    }
}
