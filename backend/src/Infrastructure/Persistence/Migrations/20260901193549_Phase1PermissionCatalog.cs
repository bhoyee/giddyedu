using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1PermissionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO giddyedu."Permissions" ("Id", "Name", "Description") VALUES
                    ('61f39ee8-9fa4-45c3-a786-ceb360eb8441', 'Schools.View', 'View the school profile and campuses'),
                    ('3b4f3305-83a6-470b-86f8-10d09cf8e930', 'Schools.Manage', 'Manage the school profile and campuses'),
                    ('9ccdb408-654f-4662-8628-b9257bd23082', 'Academics.View', 'View academic structure'),
                    ('9579917d-015b-43ae-886d-4beb5a0b557f', 'Academics.Manage', 'Manage academic structure')
                ON CONFLICT ("Name") DO NOTHING;

                INSERT INTO giddyedu."RolePermissions" ("TenantId", "RoleId", "PermissionId")
                SELECT role."TenantId", role."Id", permission."Id"
                FROM giddyedu."TenantRoles" role
                CROSS JOIN giddyedu."Permissions" permission
                WHERE role."IsSystemTemplate" = TRUE
                  AND role."Name" = 'Tenant Administrator'
                  AND permission."Name" IN ('Schools.View', 'Schools.Manage', 'Academics.View', 'Academics.Manage')
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM giddyedu."RolePermissions"
                WHERE "PermissionId" IN (
                    '61f39ee8-9fa4-45c3-a786-ceb360eb8441',
                    '3b4f3305-83a6-470b-86f8-10d09cf8e930',
                    '9ccdb408-654f-4662-8628-b9257bd23082',
                    '9579917d-015b-43ae-886d-4beb5a0b557f');
                DELETE FROM giddyedu."Permissions"
                WHERE "Id" IN (
                    '61f39ee8-9fa4-45c3-a786-ceb360eb8441',
                    '3b4f3305-83a6-470b-86f8-10d09cf8e930',
                    '9ccdb408-654f-4662-8628-b9257bd23082',
                    '9579917d-015b-43ae-886d-4beb5a0b557f');
                """);
        }
    }
}
