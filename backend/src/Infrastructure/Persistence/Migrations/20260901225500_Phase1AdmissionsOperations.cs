using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1AdmissionsOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmissionInterviews",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OutcomeNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionInterviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionInterviews_Applicants_TenantId_ApplicantId",
                        columns: x => new { x.TenantId, x.ApplicantId },
                        principalSchema: "giddyedu",
                        principalTable: "Applicants",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionReviews",
                schema: "giddyedu",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ScreeningNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionReviews", x => new { x.TenantId, x.ApplicantId });
                    table.CheckConstraint("CK_AdmissionReviews_Score", "\"Score\" >= 0 AND \"Score\" <= 100");
                    table.ForeignKey(
                        name: "FK_AdmissionReviews_Applicants_TenantId_ApplicantId",
                        columns: x => new { x.TenantId, x.ApplicantId },
                        principalSchema: "giddyedu",
                        principalTable: "Applicants",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionInterviews_TenantId_ApplicantId_ScheduledAtUtc",
                schema: "giddyedu",
                table: "AdmissionInterviews",
                columns: new[] { "TenantId", "ApplicantId", "ScheduledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionInterviews",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "AdmissionReviews",
                schema: "giddyedu");
        }
    }
}
