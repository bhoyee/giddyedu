using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1EnrollmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_TenantId_StudentId_AcademicYearId",
                schema: "giddyedu",
                table: "Enrollments");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_TenantId_StudentId_AcademicYearId",
                schema: "giddyedu",
                table: "Enrollments",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" },
                unique: true,
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_TenantId_StudentId_AcademicYearId",
                schema: "giddyedu",
                table: "Enrollments");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_TenantId_StudentId_AcademicYearId",
                schema: "giddyedu",
                table: "Enrollments",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" },
                unique: true);
        }
    }
}
