using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1StudentAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "giddyedu",
                table: "Students",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "giddyedu",
                table: "Students",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId_UserId",
                schema: "giddyedu",
                table: "Students",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Students_UserId",
                schema: "giddyedu",
                table: "Students",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_AspNetUsers_UserId",
                schema: "giddyedu",
                table: "Students",
                column: "UserId",
                principalSchema: "giddyedu",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_AspNetUsers_UserId",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_TenantId_UserId",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_UserId",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "giddyedu",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "giddyedu",
                table: "Students");
        }
    }
}
