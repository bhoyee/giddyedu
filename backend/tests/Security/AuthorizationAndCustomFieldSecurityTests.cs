using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.Identity.Domain;
using GiddyEdu.Modules.Platform.Domain;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.SecurityTests;

public sealed class AuthorizationAndCustomFieldSecurityTests
{
    [Fact]
    public async Task RoleLifecycle_SupportsCreationAssignmentInspectionAndRemoval()
    {
        await using var fixture = await SecurityFixture.CreateAsync(Permissions.RolesManage, Permissions.UsersManage);
        var service = fixture.RoleService();
        var roleId = await service.CreateRoleAsync(fixture.ActorUserId, "Registrar", [fixture.PermissionId(Permissions.UsersManage)]);
        await service.RenameRoleAsync(fixture.ActorUserId, roleId, "Senior Registrar");
        await service.ReplacePermissionsAsync(fixture.ActorUserId, roleId, [fixture.PermissionId(Permissions.UsersManage)]);
        await service.AssignRoleAsync(fixture.ActorUserId, fixture.TargetMembershipId, roleId);
        var effective = await service.GetEffectivePermissionsAsync(fixture.ActorUserId, fixture.TargetMembershipId);
        Assert.Contains(Permissions.UsersManage, effective.Permissions);
        await service.RemoveRoleAssignmentAsync(fixture.ActorUserId, fixture.TargetMembershipId, roleId);
        Assert.Empty((await service.GetEffectivePermissionsAsync(fixture.ActorUserId, fixture.TargetMembershipId)).Permissions);
        await service.DeleteRoleAsync(fixture.ActorUserId, roleId);
        Assert.DoesNotContain(await service.ListRolesAsync(), x => x.Id == roleId);
    }

    [Fact]
    public async Task RoleManagement_PreventsGrantingPermissionActorDoesNotPossess()
    {
        await using var fixture = await SecurityFixture.CreateAsync(Permissions.RolesManage);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.RoleService().CreateRoleAsync(
            fixture.ActorUserId, "Escalated", [fixture.PermissionId(Permissions.UsersManage)]));
    }

    [Fact]
    public async Task RoleManagement_PreservesLastRoleManager()
    {
        await using var fixture = await SecurityFixture.CreateAsync(Permissions.RolesManage);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RoleService().RemoveRoleAssignmentAsync(
            fixture.ActorUserId, fixture.ActorMembershipId, fixture.AdministratorRoleId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RoleService().ReplacePermissionsAsync(
            fixture.ActorUserId, fixture.AdministratorRoleId, []));
    }

    [Fact]
    public async Task RoleManagement_AllowsRemovingOneOfMultipleRoleManagerAssignments()
    {
        await using var fixture = await SecurityFixture.CreateAsync(Permissions.RolesManage);
        var service = fixture.RoleService();
        var secondRole = await service.CreateRoleAsync(fixture.ActorUserId, "Second administrator", [fixture.PermissionId(Permissions.RolesManage)]);
        await service.AssignRoleAsync(fixture.ActorUserId, fixture.ActorMembershipId, secondRole);
        await service.RemoveRoleAssignmentAsync(fixture.ActorUserId, fixture.ActorMembershipId, fixture.AdministratorRoleId);
        Assert.Contains(Permissions.RolesManage, await new PermissionService(fixture.Db, fixture.Context).GetEffectivePermissionsAsync(fixture.ActorUserId));
    }

    [Fact]
    public async Task RoleManagement_RejectsCrossTenantMembershipAndRole()
    {
        await using var fixture = await SecurityFixture.CreateAsync(Permissions.RolesManage);
        var otherTenant = Guid.NewGuid(); var otherMembership = Guid.NewGuid(); var otherRole = Guid.NewGuid();
        fixture.Context.Set(otherTenant, null);
        fixture.Db.Tenants.Add(new Tenant(otherTenant, "Other", $"other-{otherTenant:N}", fixture.Clock.UtcNow));
        fixture.Db.TenantMemberships.Add(new TenantMembership(otherMembership, otherTenant, Guid.NewGuid(), fixture.Clock.UtcNow));
        fixture.Db.TenantRoles.Add(new TenantRole(otherRole, otherTenant, "Other role")); await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear(); fixture.Context.Set(fixture.TenantId, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RoleService().AssignRoleAsync(fixture.ActorUserId, otherMembership, otherRole));
    }

    [Fact]
    public async Task CustomFields_SupportDefinitionOptionsValidationAndValues()
    {
        await using var fixture = await SecurityFixture.CreateAsync(Permissions.CustomFieldsManage);
        var service = fixture.CustomFields();
        var definitionId = await service.CreateDefinitionAsync(fixture.ActorUserId, Input("pickup_location", CustomFieldDataType.SingleSelect,
            [new("north", "North gate", 1), new("south", "South gate", 2)]));
        await service.UpsertValueAsync(fixture.ActorUserId, definitionId, fixture.CampusId, Json("\"north\""));
        var updated = Input("pickup_location", CustomFieldDataType.SingleSelect, [new("north", "North gate", 1), new("south", "South gate", 2)]) with { Label = "Pickup point" };
        await service.UpdateDefinitionAsync(fixture.ActorUserId, definitionId, updated);
        var values = await service.GetValuesAsync(fixture.CampusId, "Campus");
        Assert.Single(values); Assert.Equal("\"north\"", values.Single().ValueJson);
        Assert.Equal("Pickup point", (await service.ListDefinitionsAsync("Campus")).Single().Label);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteDefinitionAsync(fixture.ActorUserId, definitionId));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpsertValueAsync(fixture.ActorUserId, definitionId, fixture.CampusId, Json("\"unknown\"")));
    }

    [Theory]
    [InlineData("tenantid")]
    [InlineData("tenant_id")]
    [InlineData("password_hash")]
    [InlineData("paymentstatus")]
    [InlineData("grade")]
    public async Task CustomFields_RejectReservedSecurityAndCalculatedConcepts(string key)
    {
        await using var fixture = await SecurityFixture.CreateAsync(Permissions.CustomFieldsManage);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.CustomFields().CreateDefinitionAsync(fixture.ActorUserId, Input(key, CustomFieldDataType.ShortText, [])));
    }

    [Fact]
    public async Task CustomFields_RejectUnsupportedTargetsAndCrossTenantEntities()
    {
        await using var fixture = await SecurityFixture.CreateAsync(Permissions.CustomFieldsManage);
        var unsupported = Input("secret", CustomFieldDataType.ShortText, []) with { TargetModule = "Identity", TargetEntityType = "PlatformUser" };
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.CustomFields().CreateDefinitionAsync(fixture.ActorUserId, unsupported));
        var definitionId = await fixture.CustomFields().CreateDefinitionAsync(fixture.ActorUserId, Input("local_code", CustomFieldDataType.ShortText, []));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CustomFields().UpsertValueAsync(fixture.ActorUserId, definitionId, Guid.NewGuid(), Json("\"x\"")));
    }

    [Fact]
    public async Task CustomFields_EnforceRequiredAndConfiguredValidation()
    {
        await using var fixture = await SecurityFixture.CreateAsync(Permissions.CustomFieldsManage);
        var input = Input("local_identifier", CustomFieldDataType.ShortText, []) with { IsRequired = true, Validation = Json("{\"minLength\":3,\"maxLength\":5}") };
        var id = await fixture.CustomFields().CreateDefinitionAsync(fixture.ActorUserId, input);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.CustomFields().UpsertValueAsync(fixture.ActorUserId, id, fixture.CampusId, Json("\"ab\"")));
        await fixture.CustomFields().UpsertValueAsync(fixture.ActorUserId, id, fixture.CampusId, Json("\"abcd\""));
    }

    private static CustomFieldDefinitionInput Input(string key, CustomFieldDataType type, IReadOnlyCollection<CustomFieldOptionInput> options) =>
        new("Tenancy", "Campus", key, "Test field", null, type, false, true, 0, null, null, options);
    private static JsonElement Json(string value) => JsonDocument.Parse(value).RootElement.Clone();

    private sealed class SecurityFixture : IAsyncDisposable
    {
        private readonly Dictionary<string, Guid> permissionIds;
        private SecurityFixture(GiddyEduDbContext db, TenantContextAccessor context, Guid tenantId, Guid campusId, Guid actorUserId,
            Guid actorMembershipId, Guid targetMembershipId, Guid administratorRoleId, Dictionary<string, Guid> permissionIds)
        { Db = db; Context = context; TenantId = tenantId; CampusId = campusId; ActorUserId = actorUserId; ActorMembershipId = actorMembershipId; TargetMembershipId = targetMembershipId; AdministratorRoleId = administratorRoleId; this.permissionIds = permissionIds; }
        public GiddyEduDbContext Db { get; } public TenantContextAccessor Context { get; } public Guid TenantId { get; } public Guid CampusId { get; }
        public Guid ActorUserId { get; } public Guid ActorMembershipId { get; } public Guid TargetMembershipId { get; } public Guid AdministratorRoleId { get; }
        public IClock Clock { get; } = new SystemClock();
        public Guid PermissionId(string name) => permissionIds[name];
        public TenantRoleService RoleService() => new(Db, Context, new PermissionService(Db, Context));
        public CustomFieldService CustomFields() => new(Db, Context, new PermissionService(Db, Context), new CustomFieldValueValidator(), new CustomFieldTargetRegistry(Db, Context), Clock);
        public ValueTask DisposeAsync() => Db.DisposeAsync();

        public static async Task<SecurityFixture> CreateAsync(params string[] actorPermissions)
        {
            var context = new TenantContextAccessor(); var tenantId = Guid.NewGuid(); var campusId = Guid.NewGuid();
            var actorUserId = Guid.NewGuid(); var targetUserId = Guid.NewGuid(); var actorMembership = Guid.NewGuid(); var targetMembership = Guid.NewGuid(); var adminRole = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var db = new GiddyEduDbContext(options, context); context.Set(tenantId, null);
            db.Tenants.Add(new Tenant(tenantId, "School", $"school-{tenantId:N}", DateTimeOffset.UtcNow));
            db.Campuses.Add(new Campus(campusId, tenantId, "Main", "MAIN", DateTimeOffset.UtcNow));
            db.TenantMemberships.AddRange(new TenantMembership(actorMembership, tenantId, actorUserId, DateTimeOffset.UtcNow), new TenantMembership(targetMembership, tenantId, targetUserId, DateTimeOffset.UtcNow));
            var ids = Permissions.Foundation.ToDictionary(x => x, _ => Guid.NewGuid());
            db.Permissions.AddRange(ids.Select(x => new Permission(x.Value, x.Key, x.Key)));
            db.TenantRoles.Add(new TenantRole(adminRole, tenantId, "Administrator", true));
            db.RolePermissions.AddRange(actorPermissions.Select(x => new RolePermission(tenantId, adminRole, ids[x])));
            db.TenantMembershipRoles.Add(new TenantMembershipRole(tenantId, actorMembership, adminRole)); await db.SaveChangesAsync();
            return new SecurityFixture(db, context, tenantId, campusId, actorUserId, actorMembership, targetMembership, adminRole, ids);
        }
    }
}
