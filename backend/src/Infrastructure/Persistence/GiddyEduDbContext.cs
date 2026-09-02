using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.Subscriptions.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using GiddyEdu.Modules.Academics.Domain;
using GiddyEdu.Modules.Schools.Domain;
using GiddyEdu.Modules.Hr.Domain;
using GiddyEdu.Modules.StudentLifecycle.Domain;
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
    public DbSet<AccountInvitation> AccountInvitations => Set<AccountInvitation>();
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
    public DbSet<SchoolProfile> SchoolProfiles => Set<SchoolProfile>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<EducationStage> EducationStages => Set<EducationStage>();
    public DbSet<ClassLevel> ClassLevels => Set<ClassLevel>();
    public DbSet<ClassSection> ClassSections => Set<ClassSection>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<ClassSubject> ClassSubjects => Set<ClassSubject>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StaffSensitiveRecord> StaffSensitiveRecords => Set<StaffSensitiveRecord>();
    public DbSet<TeachingAssignment> TeachingAssignments => Set<TeachingAssignment>();
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<ApplicantSensitiveRecord> ApplicantSensitiveRecords => Set<ApplicantSensitiveRecord>();
    public DbSet<StudentSensitiveRecord> StudentSensitiveRecords => Set<StudentSensitiveRecord>();
    public DbSet<AdmissionReview> AdmissionReviews => Set<AdmissionReview>();
    public DbSet<AdmissionInterview> AdmissionInterviews => Set<AdmissionInterview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("giddyedu");
        ConfigureTenancy(modelBuilder);
        ConfigureIdentity(modelBuilder);
        ConfigureSubscriptions(modelBuilder);
        ConfigurePlatform(modelBuilder);
        ConfigureSchools(modelBuilder);
        ConfigureAcademics(modelBuilder);
        ConfigureHr(modelBuilder);
        ConfigureStudentLifecycle(modelBuilder);
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
        modelBuilder.Entity<AccountInvitation>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
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
        modelBuilder.Entity<SchoolProfile>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<AcademicYear>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<AcademicTerm>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<EducationStage>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ClassLevel>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ClassSection>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Department>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Subject>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ClassSubject>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Position>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<StaffProfile>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<StaffSensitiveRecord>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TeachingAssignment>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Applicant>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Student>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Guardian>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<StudentGuardian>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Enrollment>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ApplicantSensitiveRecord>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<StudentSensitiveRecord>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<AdmissionReview>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<AdmissionInterview>().HasQueryFilter(x => tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId);
    }

    private static void ConfigureTenancy(ModelBuilder b)
    {
        b.Entity<Tenant>(e => { e.ToTable("Tenants"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(200); e.Property(x => x.Slug).HasMaxLength(100); e.HasIndex(x => x.Slug).IsUnique(); });
        b.Entity<Campus>(e => { e.ToTable("Campuses"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(200); e.Property(x => x.Code).HasMaxLength(50); e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); });
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
        b.Entity<AccountInvitation>(e => { e.ToTable("AccountInvitations"); e.HasKey(x => x.Id); e.Property(x => x.Email).HasMaxLength(320); e.Property(x => x.TokenHash).HasMaxLength(64); e.HasIndex(x => x.TokenHash).IsUnique(); e.HasIndex(x => new { x.TenantId, x.TargetType, x.TargetId }); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); });
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

    private static void ConfigureSchools(ModelBuilder b)
    {
        b.Entity<SchoolProfile>(e =>
        {
            e.ToTable("SchoolProfiles"); e.HasKey(x => x.TenantId); e.Property(x => x.DisplayName).HasMaxLength(200); e.Property(x => x.LegalName).HasMaxLength(250);
            e.Property(x => x.Email).HasMaxLength(320); e.Property(x => x.Phone).HasMaxLength(30); e.Property(x => x.WebsiteUrl).HasMaxLength(500); e.Property(x => x.Address).HasMaxLength(1000);
            e.Property(x => x.CountryCode).HasMaxLength(2); e.Property(x => x.TimeZone).HasMaxLength(100); e.Property(x => x.CurrencyCode).HasMaxLength(3);
            e.Property(x => x.PrimaryColor).HasMaxLength(7); e.Property(x => x.SecondaryColor).HasMaxLength(7);
            e.HasOne<Tenant>().WithOne().HasForeignKey<SchoolProfile>(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAcademics(ModelBuilder b)
    {
        b.Entity<AcademicYear>(e => { e.ToTable("AcademicYears", t => t.HasCheckConstraint("CK_AcademicYears_DateRange", "\"EndsOn\" > \"StartsOn\"")); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(100); e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique(); e.HasIndex(x => x.TenantId).HasFilter("\"Status\" = 1").IsUnique(); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<AcademicTerm>(e => { e.ToTable("AcademicTerms", t => { t.HasCheckConstraint("CK_AcademicTerms_DateRange", "\"EndsOn\" > \"StartsOn\""); t.HasCheckConstraint("CK_AcademicTerms_Sequence", "\"Sequence\" > 0"); }); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(100); e.Property(x => x.Code).HasMaxLength(50); e.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code }).IsUnique(); e.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Sequence }).IsUnique(); e.HasOne<AcademicYear>().WithMany().HasForeignKey(x => new { x.TenantId, x.AcademicYearId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<EducationStage>(e => { e.ToTable("EducationStages"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(100); e.Property(x => x.Code).HasMaxLength(50); e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<ClassLevel>(e => { e.ToTable("ClassLevels"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(100); e.Property(x => x.Code).HasMaxLength(50); e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); e.HasOne<EducationStage>().WithMany().HasForeignKey(x => new { x.TenantId, x.EducationStageId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<ClassSection>(e => { e.ToTable("ClassSections", t => t.HasCheckConstraint("CK_ClassSections_Capacity", "\"Capacity\" IS NULL OR \"Capacity\" > 0")); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(100); e.Property(x => x.Code).HasMaxLength(50); e.HasIndex(x => new { x.TenantId, x.CampusId, x.AcademicYearId, x.Code }).IsUnique(); e.HasOne<Campus>().WithMany().HasForeignKey(x => new { x.TenantId, x.CampusId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); e.HasOne<AcademicYear>().WithMany().HasForeignKey(x => new { x.TenantId, x.AcademicYearId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); e.HasOne<ClassLevel>().WithMany().HasForeignKey(x => new { x.TenantId, x.ClassLevelId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<Department>(e => { e.ToTable("Departments"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(150); e.Property(x => x.Code).HasMaxLength(50); e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<Subject>(e => { e.ToTable("Subjects"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(150); e.Property(x => x.Code).HasMaxLength(50); e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); e.HasOne<Department>().WithMany().HasForeignKey(x => new { x.TenantId, x.DepartmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false); });
        b.Entity<ClassSubject>(e => { e.ToTable("ClassSubjects"); e.HasKey(x => new { x.TenantId, x.ClassSectionId, x.SubjectId }); e.HasOne<ClassSection>().WithMany().HasForeignKey(x => new { x.TenantId, x.ClassSectionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); e.HasOne<Subject>().WithMany().HasForeignKey(x => new { x.TenantId, x.SubjectId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); });
    }

    private static void ConfigureHr(ModelBuilder b)
    {
        b.Entity<Position>(e => { e.ToTable("Positions"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(150); e.Property(x => x.Code).HasMaxLength(50); e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<StaffProfile>(e =>
        {
            e.ToTable("StaffProfiles", t => t.HasCheckConstraint("CK_StaffProfiles_ExitDate", "\"ExitDate\" IS NULL OR \"ExitDate\" >= \"HireDate\"")); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.Property(x => x.StaffNumber).HasMaxLength(50); e.Property(x => x.FirstName).HasMaxLength(100); e.Property(x => x.LastName).HasMaxLength(100); e.Property(x => x.WorkEmail).HasMaxLength(320); e.Property(x => x.Phone).HasMaxLength(30);
            e.HasIndex(x => new { x.TenantId, x.StaffNumber }).IsUnique(); e.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Campus>().WithMany().HasForeignKey(x => new { x.TenantId, x.CampusId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => new { x.TenantId, x.DepartmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            e.HasOne<Position>().WithMany().HasForeignKey(x => new { x.TenantId, x.PositionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            e.HasOne<PlatformUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });
        b.Entity<StaffSensitiveRecord>(e => { e.ToTable("StaffSensitiveRecords"); e.HasKey(x => new { x.TenantId, x.StaffId }); e.Property(x => x.Address).HasMaxLength(1000); e.Property(x => x.NextOfKinName).HasMaxLength(200); e.Property(x => x.NextOfKinPhone).HasMaxLength(30); e.Property(x => x.Notes).HasMaxLength(2000); e.HasOne<StaffProfile>().WithOne().HasForeignKey<StaffSensitiveRecord>(x => new { x.TenantId, x.StaffId }).HasPrincipalKey<StaffProfile>(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<TeachingAssignment>(e => { e.ToTable("TeachingAssignments", table => table.HasCheckConstraint("CK_TeachingAssignments_RoleSubject", "(\"Role\" = 0 AND \"SubjectId\" IS NOT NULL) OR (\"Role\" <> 0 AND \"SubjectId\" IS NULL)")); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.HasIndex(x => new { x.TenantId, x.StaffId, x.ClassSectionId, x.SubjectId, x.Role }).IsUnique().HasFilter("\"Role\" = 0"); e.HasIndex(x => new { x.TenantId, x.ClassSectionId, x.Role }).IsUnique().HasFilter("\"Role\" IN (1, 2)"); e.HasOne<StaffProfile>().WithMany().HasForeignKey(x => new { x.TenantId, x.StaffId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); e.HasOne<ClassSection>().WithMany().HasForeignKey(x => new { x.TenantId, x.ClassSectionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); e.HasOne<Subject>().WithMany().HasForeignKey(x => new { x.TenantId, x.SubjectId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false); });
    }

    private static void ConfigureStudentLifecycle(ModelBuilder b)
    {
        b.Entity<Applicant>(e => { e.ToTable("Applicants"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.ApplicationNumber).HasMaxLength(50); e.Property(x => x.FirstName).HasMaxLength(100); e.Property(x => x.LastName).HasMaxLength(100); e.Property(x => x.Email).HasMaxLength(320); e.Property(x => x.Phone).HasMaxLength(30); e.Property(x => x.PreviousSchool).HasMaxLength(200); e.Property(x => x.Source).HasMaxLength(100); e.HasIndex(x => new { x.TenantId, x.ApplicationNumber }).IsUnique(); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<Student>(e => { e.ToTable("Students"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.AdmissionNumber).HasMaxLength(50); e.Property(x => x.FirstName).HasMaxLength(100); e.Property(x => x.LastName).HasMaxLength(100); e.HasIndex(x => new { x.TenantId, x.AdmissionNumber }).IsUnique(); e.HasIndex(x => new { x.TenantId, x.SourceApplicantId }).IsUnique().HasFilter("\"SourceApplicantId\" IS NOT NULL"); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); e.HasOne<Applicant>().WithOne().HasForeignKey<Student>(x => new { x.TenantId, x.SourceApplicantId }).HasPrincipalKey<Applicant>(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false); });
        b.Entity<Guardian>(e => { e.ToTable("Guardians"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.FirstName).HasMaxLength(100); e.Property(x => x.LastName).HasMaxLength(100); e.Property(x => x.Phone).HasMaxLength(30); e.Property(x => x.Email).HasMaxLength(320); e.HasIndex(x => new { x.TenantId, x.Phone }); e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); e.HasOne<PlatformUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).IsRequired(false); });
        b.Entity<StudentGuardian>(e => { e.ToTable("StudentGuardians"); e.HasKey(x => new { x.TenantId, x.StudentId, x.GuardianId }); e.HasOne<Student>().WithMany().HasForeignKey(x => new { x.TenantId, x.StudentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); e.HasOne<Guardian>().WithMany().HasForeignKey(x => new { x.TenantId, x.GuardianId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<Enrollment>(e => { e.ToTable("Enrollments"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.StudentId, x.AcademicYearId }).IsUnique().HasFilter("\"Status\" = 0"); e.HasOne<Student>().WithMany().HasForeignKey(x => new { x.TenantId, x.StudentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); e.HasOne<AcademicYear>().WithMany().HasForeignKey(x => new { x.TenantId, x.AcademicYearId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); e.HasOne<ClassSection>().WithMany().HasForeignKey(x => new { x.TenantId, x.ClassSectionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<ApplicantSensitiveRecord>(e => { e.ToTable("ApplicantSensitiveRecords"); e.HasKey(x => new { x.TenantId, x.ApplicantId }); e.Property(x => x.Address).HasMaxLength(1000); e.Property(x => x.MedicalInformation).HasMaxLength(2000); e.Property(x => x.Allergies).HasMaxLength(1000); e.Property(x => x.SpecialEducationalNeeds).HasMaxLength(2000); e.HasOne<Applicant>().WithOne().HasForeignKey<ApplicantSensitiveRecord>(x => new { x.TenantId, x.ApplicantId }).HasPrincipalKey<Applicant>(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<StudentSensitiveRecord>(e => { e.ToTable("StudentSensitiveRecords"); e.HasKey(x => new { x.TenantId, x.StudentId }); e.Property(x => x.Address).HasMaxLength(1000); e.Property(x => x.MedicalInformation).HasMaxLength(2000); e.Property(x => x.Allergies).HasMaxLength(1000); e.Property(x => x.SpecialEducationalNeeds).HasMaxLength(2000); e.Property(x => x.PrivateNotes).HasMaxLength(2000); e.HasOne<Student>().WithOne().HasForeignKey<StudentSensitiveRecord>(x => new { x.TenantId, x.StudentId }).HasPrincipalKey<Student>(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<AdmissionReview>(e => { e.ToTable("AdmissionReviews", table => table.HasCheckConstraint("CK_AdmissionReviews_Score", "\"Score\" >= 0 AND \"Score\" <= 100")); e.HasKey(x => new { x.TenantId, x.ApplicantId }); e.Property(x => x.Score).HasPrecision(5, 2); e.Property(x => x.ScreeningNotes).HasMaxLength(2000); e.HasOne<Applicant>().WithOne().HasForeignKey<AdmissionReview>(x => new { x.TenantId, x.ApplicantId }).HasPrincipalKey<Applicant>(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<AdmissionInterview>(e => { e.ToTable("AdmissionInterviews"); e.HasKey(x => x.Id); e.Property(x => x.Location).HasMaxLength(300); e.Property(x => x.OutcomeNotes).HasMaxLength(2000); e.HasIndex(x => new { x.TenantId, x.ApplicantId, x.ScheduledAtUtc }); e.HasOne<Applicant>().WithMany().HasForeignKey(x => new { x.TenantId, x.ApplicantId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); });
    }
}
