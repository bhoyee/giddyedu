using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianBin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                schema: "giddyedu",
                table: "Guardians",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                schema: "giddyedu",
                table: "Guardians",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MembershipSuspendedForBin",
                schema: "giddyedu",
                table: "Guardians",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SuspendedParentRoleId",
                schema: "giddyedu",
                table: "Guardians",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guardians_TenantId_DeletedAtUtc",
                schema: "giddyedu",
                table: "Guardians",
                columns: new[] { "TenantId", "DeletedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Guardians_TenantId_DeletedAtUtc",
                schema: "giddyedu",
                table: "Guardians");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "giddyedu",
                table: "Guardians");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                schema: "giddyedu",
                table: "Guardians");

            migrationBuilder.DropColumn(
                name: "MembershipSuspendedForBin",
                schema: "giddyedu",
                table: "Guardians");

            migrationBuilder.DropColumn(
                name: "SuspendedParentRoleId",
                schema: "giddyedu",
                table: "Guardians");
        }
    }
}
