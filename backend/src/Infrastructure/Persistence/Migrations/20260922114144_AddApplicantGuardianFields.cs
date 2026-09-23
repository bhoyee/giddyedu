using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantGuardianFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuardianEmail",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianPhone",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianRelationship",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuardianEmail",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "GuardianPhone",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "GuardianRelationship",
                schema: "giddyedu",
                table: "Applicants");
        }
    }
}
