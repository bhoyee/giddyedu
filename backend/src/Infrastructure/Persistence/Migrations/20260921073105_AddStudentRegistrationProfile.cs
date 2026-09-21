using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentRegistrationProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gender",
                schema: "giddyedu",
                table: "Students",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                schema: "giddyedu",
                table: "Students",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "giddyedu",
                table: "Students",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentType",
                schema: "giddyedu",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                schema: "giddyedu",
                table: "Guardians",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudentType",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "giddyedu",
                table: "Guardians");
        }
    }
}
