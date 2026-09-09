using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentProgressions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentProgressions",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProgressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentProgressions_Enrollments_FromEnrollmentId",
                        column: x => x.FromEnrollmentId,
                        principalSchema: "giddyedu",
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentProgressions_Enrollments_ToEnrollmentId",
                        column: x => x.ToEnrollmentId,
                        principalSchema: "giddyedu",
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentProgressions_Students_TenantId_StudentId",
                        columns: x => new { x.TenantId, x.StudentId },
                        principalSchema: "giddyedu",
                        principalTable: "Students",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentProgressions_FromEnrollmentId",
                schema: "giddyedu",
                table: "StudentProgressions",
                column: "FromEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProgressions_TenantId_StudentId_CreatedAtUtc",
                schema: "giddyedu",
                table: "StudentProgressions",
                columns: new[] { "TenantId", "StudentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentProgressions_ToEnrollmentId",
                schema: "giddyedu",
                table: "StudentProgressions",
                column: "ToEnrollmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentProgressions",
                schema: "giddyedu");
        }
    }
}
