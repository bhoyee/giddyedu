using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmissionOffers",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Response = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionOffers_Applicants_TenantId_ApplicantId",
                        columns: x => new { x.TenantId, x.ApplicantId },
                        principalSchema: "giddyedu",
                        principalTable: "Applicants",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionOffers_TenantId_ApplicantId_CreatedAtUtc",
                schema: "giddyedu",
                table: "AdmissionOffers",
                columns: new[] { "TenantId", "ApplicantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionOffers_TokenHash",
                schema: "giddyedu",
                table: "AdmissionOffers",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionOffers",
                schema: "giddyedu");
        }
    }
}
