using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentMedicalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BloodGroup",
                schema: "giddyedu",
                table: "StudentSensitiveRecords",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Disability",
                schema: "giddyedu",
                table: "StudentSensitiveRecords",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Genotype",
                schema: "giddyedu",
                table: "StudentSensitiveRecords",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightCm",
                schema: "giddyedu",
                table: "StudentSensitiveRecords",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                schema: "giddyedu",
                table: "StudentSensitiveRecords",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BloodGroup",
                schema: "giddyedu",
                table: "StudentSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "Disability",
                schema: "giddyedu",
                table: "StudentSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "Genotype",
                schema: "giddyedu",
                table: "StudentSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "HeightCm",
                schema: "giddyedu",
                table: "StudentSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                schema: "giddyedu",
                table: "StudentSensitiveRecords");
        }
    }
}
