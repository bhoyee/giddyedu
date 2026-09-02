using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1StudentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applicants",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PreviousSchool = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applicants", x => x.Id);
                    table.UniqueConstraint("AK_Applicants_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Applicants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "giddyedu",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Guardians",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guardians", x => x.Id);
                    table.UniqueConstraint("AK_Guardians_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Guardians_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "giddyedu",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Guardians_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "giddyedu",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceApplicantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.UniqueConstraint("AK_Students_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Students_Applicants_TenantId_SourceApplicantId",
                        columns: x => new { x.TenantId, x.SourceApplicantId },
                        principalSchema: "giddyedu",
                        principalTable: "Applicants",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "giddyedu",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrolledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enrollments_AcademicYears_TenantId_AcademicYearId",
                        columns: x => new { x.TenantId, x.AcademicYearId },
                        principalSchema: "giddyedu",
                        principalTable: "AcademicYears",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_ClassSections_TenantId_ClassSectionId",
                        columns: x => new { x.TenantId, x.ClassSectionId },
                        principalSchema: "giddyedu",
                        principalTable: "ClassSections",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_Students_TenantId_StudentId",
                        columns: x => new { x.TenantId, x.StudentId },
                        principalSchema: "giddyedu",
                        principalTable: "Students",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentGuardians",
                schema: "giddyedu",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuardianId = table.Column<Guid>(type: "uuid", nullable: false),
                    Relationship = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsEmergencyContact = table.Column<bool>(type: "boolean", nullable: false),
                    MayCollect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentGuardians", x => new { x.TenantId, x.StudentId, x.GuardianId });
                    table.ForeignKey(
                        name: "FK_StudentGuardians_Guardians_TenantId_GuardianId",
                        columns: x => new { x.TenantId, x.GuardianId },
                        principalSchema: "giddyedu",
                        principalTable: "Guardians",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentGuardians_Students_TenantId_StudentId",
                        columns: x => new { x.TenantId, x.StudentId },
                        principalSchema: "giddyedu",
                        principalTable: "Students",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_TenantId_ApplicationNumber",
                schema: "giddyedu",
                table: "Applicants",
                columns: new[] { "TenantId", "ApplicationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_TenantId_AcademicYearId",
                schema: "giddyedu",
                table: "Enrollments",
                columns: new[] { "TenantId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_TenantId_ClassSectionId",
                schema: "giddyedu",
                table: "Enrollments",
                columns: new[] { "TenantId", "ClassSectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_TenantId_StudentId_AcademicYearId",
                schema: "giddyedu",
                table: "Enrollments",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guardians_TenantId_Phone",
                schema: "giddyedu",
                table: "Guardians",
                columns: new[] { "TenantId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_Guardians_UserId",
                schema: "giddyedu",
                table: "Guardians",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_TenantId_GuardianId",
                schema: "giddyedu",
                table: "StudentGuardians",
                columns: new[] { "TenantId", "GuardianId" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId_AdmissionNumber",
                schema: "giddyedu",
                table: "Students",
                columns: new[] { "TenantId", "AdmissionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId_SourceApplicantId",
                schema: "giddyedu",
                table: "Students",
                columns: new[] { "TenantId", "SourceApplicantId" },
                unique: true,
                filter: "\"SourceApplicantId\" IS NOT NULL");

            migrationBuilder.Sql("""
                INSERT INTO giddyedu."Permissions" ("Id", "Name", "Description") VALUES
                    ('a359d573-7110-41a4-8662-7964cf236724', 'Admissions.View', 'View admission applications'),
                    ('a019810a-cc77-42b1-9d31-d24661e53c5c', 'Admissions.Manage', 'Manage admission applications and conversion'),
                    ('e669c125-9818-4055-8aa4-a9e17cf96b2c', 'Students.View', 'View canonical student records'),
                    ('71f1ae0d-54eb-429c-9c44-ee2c32e45d51', 'Students.Manage', 'Manage canonical student records and enrolment'),
                    ('3520a42c-c150-43ee-aeea-abcf63717eaf', 'Guardians.View', 'View guardian records'),
                    ('083073c9-6861-4a17-9cea-a80a5cd61aac', 'Guardians.Manage', 'Manage guardians and student relationships')
                ON CONFLICT ("Name") DO NOTHING;
                INSERT INTO giddyedu."RolePermissions" ("TenantId", "RoleId", "PermissionId")
                SELECT role."TenantId", role."Id", permission."Id" FROM giddyedu."TenantRoles" role CROSS JOIN giddyedu."Permissions" permission
                WHERE role."IsSystemTemplate" = TRUE AND role."Name" = 'Tenant Administrator'
                  AND permission."Name" IN ('Admissions.View','Admissions.Manage','Students.View','Students.Manage','Guardians.View','Guardians.Manage')
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM giddyedu."RolePermissions" WHERE "PermissionId" IN ('a359d573-7110-41a4-8662-7964cf236724','a019810a-cc77-42b1-9d31-d24661e53c5c','e669c125-9818-4055-8aa4-a9e17cf96b2c','71f1ae0d-54eb-429c-9c44-ee2c32e45d51','3520a42c-c150-43ee-aeea-abcf63717eaf','083073c9-6861-4a17-9cea-a80a5cd61aac');
                DELETE FROM giddyedu."Permissions" WHERE "Id" IN ('a359d573-7110-41a4-8662-7964cf236724','a019810a-cc77-42b1-9d31-d24661e53c5c','e669c125-9818-4055-8aa4-a9e17cf96b2c','71f1ae0d-54eb-429c-9c44-ee2c32e45d51','3520a42c-c150-43ee-aeea-abcf63717eaf','083073c9-6861-4a17-9cea-a80a5cd61aac');
                """);
            migrationBuilder.DropTable(
                name: "Enrollments",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "StudentGuardians",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "Guardians",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "Students",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "Applicants",
                schema: "giddyedu");
        }
    }
}
