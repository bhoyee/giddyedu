using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAdditionalPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffAdditionalPositions",
                schema: "giddyedu",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAdditionalPositions", x => new { x.TenantId, x.StaffId, x.PositionId });
                    table.ForeignKey(
                        name: "FK_StaffAdditionalPositions_Positions_TenantId_PositionId",
                        columns: x => new { x.TenantId, x.PositionId },
                        principalSchema: "giddyedu",
                        principalTable: "Positions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffAdditionalPositions_StaffProfiles_TenantId_StaffId",
                        columns: x => new { x.TenantId, x.StaffId },
                        principalSchema: "giddyedu",
                        principalTable: "StaffProfiles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAdditionalPositions_TenantId_PositionId",
                schema: "giddyedu",
                table: "StaffAdditionalPositions",
                columns: new[] { "TenantId", "PositionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffAdditionalPositions",
                schema: "giddyedu");
        }
    }
}
