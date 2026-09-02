using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1AcademicIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicTerms_DateRange",
                schema: "giddyedu",
                table: "AcademicTerms",
                sql: "\"EndsOn\" > \"StartsOn\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicTerms_Sequence",
                schema: "giddyedu",
                table: "AcademicTerms",
                sql: "\"Sequence\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_Tenants_TenantId",
                schema: "giddyedu",
                table: "Subjects",
                column: "TenantId",
                principalSchema: "giddyedu",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_Tenants_TenantId",
                schema: "giddyedu",
                table: "Subjects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicTerms_DateRange",
                schema: "giddyedu",
                table: "AcademicTerms");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicTerms_Sequence",
                schema: "giddyedu",
                table: "AcademicTerms");
        }
    }
}
