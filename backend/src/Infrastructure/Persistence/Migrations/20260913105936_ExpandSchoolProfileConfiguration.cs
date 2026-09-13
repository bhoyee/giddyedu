using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandSchoolProfileConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomDomain",
                schema: "giddyedu",
                table: "SchoolProfiles",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DateFormat",
                schema: "giddyedu",
                table: "SchoolProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "dd/MM/yyyy");

            migrationBuilder.AddColumn<string>(
                name: "LocalGovernment",
                schema: "giddyedu",
                table: "SchoolProfiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "giddyedu",
                table: "SchoolProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                schema: "giddyedu",
                table: "SchoolProfiles",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeFormat",
                schema: "giddyedu",
                table: "SchoolProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "HH:mm");

            migrationBuilder.Sql("""
                UPDATE giddyedu."SchoolProfiles" AS profile
                SET "LegalName" = tenant."Name"
                FROM giddyedu."Tenants" AS tenant
                WHERE profile."TenantId" = tenant."Id"
                  AND (profile."LegalName" IS NULL OR btrim(profile."LegalName") = '');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolProfiles_CustomDomain",
                schema: "giddyedu",
                table: "SchoolProfiles",
                column: "CustomDomain",
                unique: true,
                filter: "\"CustomDomain\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolProfiles_CustomDomain",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropColumn(
                name: "CustomDomain",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropColumn(
                name: "DateFormat",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropColumn(
                name: "LocalGovernment",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropColumn(
                name: "Tagline",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropColumn(
                name: "TimeFormat",
                schema: "giddyedu",
                table: "SchoolProfiles");
        }
    }
}
