using GiddyEdu.BuildingBlocks.Tenancy;

namespace GiddyEdu.Modules.Identity.Domain;

public sealed class Permission
{
    private Permission() { }
    public Permission(Guid id, string name, string description) { Id = id; Name = name.Trim(); Description = description.Trim(); }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
}

public sealed class TenantRole : ITenantOwned
{
    private TenantRole() { }
    public TenantRole(Guid id, Guid tenantId, string name, bool isSystemTemplate = false)
    { Id = id; TenantId = tenantId; Name = name.Trim(); IsSystemTemplate = isSystemTemplate; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsSystemTemplate { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("Role name must contain between 1 and 100 characters.", nameof(name));
        Name = name.Trim();
    }
}

public sealed class RolePermission : ITenantOwned
{
    private RolePermission() { }
    public RolePermission(Guid tenantId, Guid roleId, Guid permissionId) { TenantId = tenantId; RoleId = roleId; PermissionId = permissionId; }
    public Guid TenantId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
}

public sealed class TenantMembershipRole : ITenantOwned
{
    private TenantMembershipRole() { }
    public TenantMembershipRole(Guid tenantId, Guid membershipId, Guid roleId) { TenantId = tenantId; MembershipId = membershipId; RoleId = roleId; }
    public Guid TenantId { get; private set; }
    public Guid MembershipId { get; private set; }
    public Guid RoleId { get; private set; }
}
