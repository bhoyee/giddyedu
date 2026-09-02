using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1SensitiveLearnerData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicantSensitiveRecords",
                schema: "giddyedu",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MedicalInformation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Allergies = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SpecialEducationalNeeds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantSensitiveRecords", x => new { x.TenantId, x.ApplicantId });
                    table.ForeignKey(
                        name: "FK_ApplicantSensitiveRecords_Applicants_TenantId_ApplicantId",
                        columns: x => new { x.TenantId, x.ApplicantId },
                        principalSchema: "giddyedu",
                        principalTable: "Applicants",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO giddyedu."Permissions" ("Id", "Name", "Description") VALUES
                    ('713155e9-72e4-43e6-b753-167efb30f5d0', 'Admissions.Sensitive.View', 'View sensitive applicant information'),
                    ('b0a32980-a94a-4a8d-908f-1d86ff845303', 'Admissions.Sensitive.Manage', 'Manage sensitive applicant information'),
                    ('8f4a4fe4-41e3-4e09-85db-d3c95919529a', 'Students.Sensitive.View', 'View sensitive student information'),
                    ('8e9871fb-617b-417d-bff3-b09454438120', 'Students.Sensitive.Manage', 'Manage sensitive student information')
                ON CONFLICT ("Name") DO NOTHING;
                INSERT INTO giddyedu."RolePermissions" ("TenantId", "RoleId", "PermissionId")
                SELECT role."TenantId", role."Id", permission."Id" FROM giddyedu."TenantRoles" role CROSS JOIN giddyedu."Permissions" permission
                WHERE role."IsSystemTemplate" = TRUE AND role."Name" = 'Tenant Administrator'
                  AND permission."Name" IN ('Admissions.Sensitive.View','Admissions.Sensitive.Manage','Students.Sensitive.View','Students.Sensitive.Manage')
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.CreateTable(
                name: "StudentSensitiveRecords",
                schema: "giddyedu",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MedicalInformation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Allergies = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SpecialEducationalNeeds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PrivateNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSensitiveRecords", x => new { x.TenantId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_StudentSensitiveRecords_Students_TenantId_StudentId",
                        columns: x => new { x.TenantId, x.StudentId },
                        principalSchema: "giddyedu",
                        principalTable: "Students",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM giddyedu."RolePermissions" WHERE "PermissionId" IN ('713155e9-72e4-43e6-b753-167efb30f5d0','b0a32980-a94a-4a8d-908f-1d86ff845303','8f4a4fe4-41e3-4e09-85db-d3c95919529a','8e9871fb-617b-417d-bff3-b09454438120');
                DELETE FROM giddyedu."Permissions" WHERE "Id" IN ('713155e9-72e4-43e6-b753-167efb30f5d0','b0a32980-a94a-4a8d-908f-1d86ff845303','8f4a4fe4-41e3-4e09-85db-d3c95919529a','8e9871fb-617b-417d-bff3-b09454438120');
                """);
            migrationBuilder.DropTable(
                name: "ApplicantSensitiveRecords",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "StudentSensitiveRecords",
                schema: "giddyedu");
        }
    }
}
