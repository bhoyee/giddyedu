using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantGenderAndProposedClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gender",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposedAcademicYearId",
                schema: "giddyedu",
                table: "Applicants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposedClassSectionId",
                schema: "giddyedu",
                table: "Applicants",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "ProposedAcademicYearId",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "ProposedClassSectionId",
                schema: "giddyedu",
                table: "Applicants");
        }
    }
}
