using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1TeachingAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeachingAssignments",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingAssignments", x => x.Id);
                    table.UniqueConstraint("AK_TeachingAssignments_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_TeachingAssignments_RoleSubject", "(\"Role\" = 0 AND \"SubjectId\" IS NOT NULL) OR (\"Role\" <> 0 AND \"SubjectId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_ClassSections_TenantId_ClassSectionId",
                        columns: x => new { x.TenantId, x.ClassSectionId },
                        principalSchema: "giddyedu",
                        principalTable: "ClassSections",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_StaffProfiles_TenantId_StaffId",
                        columns: x => new { x.TenantId, x.StaffId },
                        principalSchema: "giddyedu",
                        principalTable: "StaffProfiles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_Subjects_TenantId_SubjectId",
                        columns: x => new { x.TenantId, x.SubjectId },
                        principalSchema: "giddyedu",
                        principalTable: "Subjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_TenantId_ClassSectionId_Role",
                schema: "giddyedu",
                table: "TeachingAssignments",
                columns: new[] { "TenantId", "ClassSectionId", "Role" },
                unique: true,
                filter: "\"Role\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_TenantId_StaffId_ClassSectionId_Subject~",
                schema: "giddyedu",
                table: "TeachingAssignments",
                columns: new[] { "TenantId", "StaffId", "ClassSectionId", "SubjectId", "Role" },
                unique: true,
                filter: "\"Role\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_TenantId_SubjectId",
                schema: "giddyedu",
                table: "TeachingAssignments",
                columns: new[] { "TenantId", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeachingAssignments",
                schema: "giddyedu");
        }
    }
}
