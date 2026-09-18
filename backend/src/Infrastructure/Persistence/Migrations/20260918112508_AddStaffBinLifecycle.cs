using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffBinLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                schema: "giddyedu",
                table: "StaffProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                schema: "giddyedu",
                table: "StaffProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspendedRoleIdsJson",
                schema: "giddyedu",
                table: "StaffProfiles",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_TenantId_DeletedAtUtc",
                schema: "giddyedu",
                table: "StaffProfiles",
                columns: new[] { "TenantId", "DeletedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffProfiles_TenantId_DeletedAtUtc",
                schema: "giddyedu",
                table: "StaffProfiles");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "giddyedu",
                table: "StaffProfiles");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                schema: "giddyedu",
                table: "StaffProfiles");

            migrationBuilder.DropColumn(
                name: "SuspendedRoleIdsJson",
                schema: "giddyedu",
                table: "StaffProfiles");
        }
    }
}
