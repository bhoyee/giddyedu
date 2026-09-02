using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1StaffManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Positions",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                    table.UniqueConstraint("AK_Positions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Positions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "giddyedu",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffProfiles",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CampusId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffProfiles", x => x.Id);
                    table.UniqueConstraint("AK_StaffProfiles_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_StaffProfiles_ExitDate", "\"ExitDate\" IS NULL OR \"ExitDate\" >= \"HireDate\"");
                    table.ForeignKey(
                        name: "FK_StaffProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "giddyedu",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffProfiles_Campuses_TenantId_CampusId",
                        columns: x => new { x.TenantId, x.CampusId },
                        principalSchema: "giddyedu",
                        principalTable: "Campuses",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffProfiles_Departments_TenantId_DepartmentId",
                        columns: x => new { x.TenantId, x.DepartmentId },
                        principalSchema: "giddyedu",
                        principalTable: "Departments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffProfiles_Positions_TenantId_PositionId",
                        columns: x => new { x.TenantId, x.PositionId },
                        principalSchema: "giddyedu",
                        principalTable: "Positions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "giddyedu",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffSensitiveRecords",
                schema: "giddyedu",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NextOfKinName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NextOfKinPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffSensitiveRecords", x => new { x.TenantId, x.StaffId });
                    table.ForeignKey(
                        name: "FK_StaffSensitiveRecords_StaffProfiles_TenantId_StaffId",
                        columns: x => new { x.TenantId, x.StaffId },
                        principalSchema: "giddyedu",
                        principalTable: "StaffProfiles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Positions_TenantId_Code",
                schema: "giddyedu",
                table: "Positions",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_TenantId_CampusId",
                schema: "giddyedu",
                table: "StaffProfiles",
                columns: new[] { "TenantId", "CampusId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_TenantId_DepartmentId",
                schema: "giddyedu",
                table: "StaffProfiles",
                columns: new[] { "TenantId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_TenantId_PositionId",
                schema: "giddyedu",
                table: "StaffProfiles",
                columns: new[] { "TenantId", "PositionId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_TenantId_StaffNumber",
                schema: "giddyedu",
                table: "StaffProfiles",
                columns: new[] { "TenantId", "StaffNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_TenantId_UserId",
                schema: "giddyedu",
                table: "StaffProfiles",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_UserId",
                schema: "giddyedu",
                table: "StaffProfiles",
                column: "UserId");

            migrationBuilder.Sql("""
                INSERT INTO giddyedu."Permissions" ("Id", "Name", "Description") VALUES
                    ('43092f4b-0681-45ef-a5d5-51328085ddbd', 'Staff.View', 'View staff profiles and positions'),
                    ('e2fa5eb9-4af2-4e06-a91d-3558358e8adc', 'Staff.Manage', 'Manage staff profiles and positions'),
                    ('b1ba39a4-1ef0-4e1b-8bc8-1da7baa6b58d', 'Staff.Sensitive.View', 'View sensitive staff information')
                ON CONFLICT ("Name") DO NOTHING;

                INSERT INTO giddyedu."RolePermissions" ("TenantId", "RoleId", "PermissionId")
                SELECT role."TenantId", role."Id", permission."Id"
                FROM giddyedu."TenantRoles" role
                CROSS JOIN giddyedu."Permissions" permission
                WHERE role."IsSystemTemplate" = TRUE
                  AND role."Name" = 'Tenant Administrator'
                  AND permission."Name" IN ('Staff.View', 'Staff.Manage', 'Staff.Sensitive.View')
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM giddyedu."RolePermissions" WHERE "PermissionId" IN
                    ('43092f4b-0681-45ef-a5d5-51328085ddbd', 'e2fa5eb9-4af2-4e06-a91d-3558358e8adc', 'b1ba39a4-1ef0-4e1b-8bc8-1da7baa6b58d');
                DELETE FROM giddyedu."Permissions" WHERE "Id" IN
                    ('43092f4b-0681-45ef-a5d5-51328085ddbd', 'e2fa5eb9-4af2-4e06-a91d-3558358e8adc', 'b1ba39a4-1ef0-4e1b-8bc8-1da7baa6b58d');
                """);
            migrationBuilder.DropTable(
                name: "StaffSensitiveRecords",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "StaffProfiles",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "Positions",
                schema: "giddyedu");
        }
    }
}
