using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandStaffRegistrationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Achievements",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodGroup",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Disability",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Genotype",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightCm",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalGovernment",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaritalStatus",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficeAddress",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Religion",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Skills",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialProfilesJson",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                schema: "giddyedu",
                table: "StaffSensitiveRecords",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Achievements",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "BloodGroup",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "Disability",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "Genotype",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "HeightCm",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "LocalGovernment",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "OfficeAddress",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "Religion",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "Skills",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "SocialProfilesJson",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "Website",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                schema: "giddyedu",
                table: "StaffSensitiveRecords");
        }
    }
}
