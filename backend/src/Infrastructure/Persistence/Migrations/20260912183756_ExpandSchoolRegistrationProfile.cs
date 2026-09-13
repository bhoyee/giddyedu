using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandSchoolRegistrationProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoleAtSchool",
                schema: "giddyedu",
                table: "TenantMemberships",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolType",
                schema: "giddyedu",
                table: "SchoolProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsMainCampus",
                schema: "giddyedu",
                table: "Campuses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE giddyedu."SchoolProfiles"
                SET "SchoolType" = 'Not specified'
                WHERE "SchoolType" = '';

                UPDATE giddyedu."Campuses"
                SET "IsMainCampus" = TRUE
                WHERE "Code" = 'MAIN';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Campuses_TenantId",
                schema: "giddyedu",
                table: "Campuses",
                column: "TenantId",
                unique: true,
                filter: "\"IsMainCampus\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Campuses_TenantId",
                schema: "giddyedu",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "RoleAtSchool",
                schema: "giddyedu",
                table: "TenantMemberships");

            migrationBuilder.DropColumn(
                name: "SchoolType",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropColumn(
                name: "IsMainCampus",
                schema: "giddyedu",
                table: "Campuses");
        }
    }
}
