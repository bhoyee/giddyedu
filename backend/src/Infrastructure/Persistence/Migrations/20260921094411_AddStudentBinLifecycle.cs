using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentBinLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                schema: "giddyedu",
                table: "Students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                schema: "giddyedu",
                table: "Students",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MembershipSuspendedForBin",
                schema: "giddyedu",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SuspendedStudentRoleId",
                schema: "giddyedu",
                table: "Students",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MembershipSuspendedForBin",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "SuspendedStudentRoleId",
                schema: "giddyedu",
                table: "Students");
        }
    }
}
