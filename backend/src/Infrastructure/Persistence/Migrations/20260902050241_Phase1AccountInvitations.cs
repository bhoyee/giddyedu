using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1AccountInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountInvitations",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountInvitations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "giddyedu",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountInvitations_TenantId_TargetType_TargetId",
                schema: "giddyedu",
                table: "AccountInvitations",
                columns: new[] { "TenantId", "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountInvitations_TokenHash",
                schema: "giddyedu",
                table: "AccountInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountInvitations",
                schema: "giddyedu");
        }
    }
}
