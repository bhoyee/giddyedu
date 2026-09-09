using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GraceEndsAtUtc",
                schema: "giddyedu",
                table: "TenantSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "giddyedu",
                table: "TenantSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "giddyedu",
                table: "TenantSubscriptions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql("""
                UPDATE giddyedu."TenantSubscriptions"
                SET "Status" = CASE WHEN "IsActive" THEN 0 ELSE 4 END,
                    "UpdatedAtUtc" = COALESCE("EndsAtUtc", "StartsAtUtc");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_TenantId_Status",
                schema: "giddyedu",
                table: "TenantSubscriptions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantSubscriptions_Status",
                schema: "giddyedu",
                table: "TenantSubscriptions",
                sql: "\"Status\" BETWEEN 0 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantSubscriptions_StatusAccess",
                schema: "giddyedu",
                table: "TenantSubscriptions",
                sql: "(\"IsActive\" = TRUE AND \"Status\" IN (0, 1)) OR (\"IsActive\" = FALSE AND \"Status\" IN (2, 3, 4))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantSubscriptions_TenantId_Status",
                schema: "giddyedu",
                table: "TenantSubscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantSubscriptions_Status",
                schema: "giddyedu",
                table: "TenantSubscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantSubscriptions_StatusAccess",
                schema: "giddyedu",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "GraceEndsAtUtc",
                schema: "giddyedu",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "giddyedu",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "giddyedu",
                table: "TenantSubscriptions");
        }
    }
}
