using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReworkApplicantAcademicFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProposedAcademicYearId",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.RenameColumn(
                name: "ProposedClassSectionId",
                schema: "giddyedu",
                table: "Applicants",
                newName: "ProposedClassLevelId");

            migrationBuilder.AddColumn<string>(
                name: "LastClassCompleted",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LeavingDate",
                schema: "giddyedu",
                table: "Applicants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSchoolAddress",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonForLeaving",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastClassCompleted",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "LeavingDate",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "PreviousSchoolAddress",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "ReasonForLeaving",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.RenameColumn(
                name: "ProposedClassLevelId",
                schema: "giddyedu",
                table: "Applicants",
                newName: "ProposedClassSectionId");

            migrationBuilder.AddColumn<Guid>(
                name: "ProposedAcademicYearId",
                schema: "giddyedu",
                table: "Applicants",
                type: "uuid",
                nullable: true);
        }
    }
}
