using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantGuardiansTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuardianEmail",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "GuardianPhone",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "GuardianRelationship",
                schema: "giddyedu",
                table: "Applicants");

            migrationBuilder.CreateTable(
                name: "ApplicantGuardians",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Relationship = table.Column<int>(type: "integer", nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantGuardians", x => x.Id);
                    table.UniqueConstraint("AK_ApplicantGuardians_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ApplicantGuardians_Applicants_TenantId_ApplicantId",
                        columns: x => new { x.TenantId, x.ApplicantId },
                        principalSchema: "giddyedu",
                        principalTable: "Applicants",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantGuardians_TenantId_ApplicantId",
                schema: "giddyedu",
                table: "ApplicantGuardians",
                columns: new[] { "TenantId", "ApplicantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicantGuardians",
                schema: "giddyedu");

            migrationBuilder.AddColumn<string>(
                name: "GuardianEmail",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianPhone",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianRelationship",
                schema: "giddyedu",
                table: "Applicants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
