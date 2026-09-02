using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1SchoolAcademicCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Campuses_TenantId_Id",
                schema: "giddyedu",
                table: "Campuses",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "AcademicYears",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYears", x => x.Id);
                    table.UniqueConstraint("AK_AcademicYears_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AcademicYears_DateRange", "\"EndsOn\" > \"StartsOn\"");
                    table.ForeignKey(
                        name: "FK_AcademicYears_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "giddyedu",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.UniqueConstraint("AK_Departments_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Departments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "giddyedu",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EducationStages",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationStages", x => x.Id);
                    table.UniqueConstraint("AK_EducationStages_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_EducationStages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "giddyedu",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolProfiles",
                schema: "giddyedu",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    LogoFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    SecondaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolProfiles", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_SchoolProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "giddyedu",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcademicTerms",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicTerms", x => x.Id);
                    table.UniqueConstraint("AK_AcademicTerms_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AcademicTerms_AcademicYears_TenantId_AcademicYearId",
                        columns: x => new { x.TenantId, x.AcademicYearId },
                        principalSchema: "giddyedu",
                        principalTable: "AcademicYears",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsCore = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                    table.UniqueConstraint("AK_Subjects_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Subjects_Departments_TenantId_DepartmentId",
                        columns: x => new { x.TenantId, x.DepartmentId },
                        principalSchema: "giddyedu",
                        principalTable: "Departments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassLevels",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassLevels", x => x.Id);
                    table.UniqueConstraint("AK_ClassLevels_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ClassLevels_EducationStages_TenantId_EducationStageId",
                        columns: x => new { x.TenantId, x.EducationStageId },
                        principalSchema: "giddyedu",
                        principalTable: "EducationStages",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassSections",
                schema: "giddyedu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampusId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassLevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSections", x => x.Id);
                    table.UniqueConstraint("AK_ClassSections_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ClassSections_Capacity", "\"Capacity\" IS NULL OR \"Capacity\" > 0");
                    table.ForeignKey(
                        name: "FK_ClassSections_AcademicYears_TenantId_AcademicYearId",
                        columns: x => new { x.TenantId, x.AcademicYearId },
                        principalSchema: "giddyedu",
                        principalTable: "AcademicYears",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSections_Campuses_TenantId_CampusId",
                        columns: x => new { x.TenantId, x.CampusId },
                        principalSchema: "giddyedu",
                        principalTable: "Campuses",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSections_ClassLevels_TenantId_ClassLevelId",
                        columns: x => new { x.TenantId, x.ClassLevelId },
                        principalSchema: "giddyedu",
                        principalTable: "ClassLevels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassSubjects",
                schema: "giddyedu",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCompulsory = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSubjects", x => new { x.TenantId, x.ClassSectionId, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_ClassSubjects_ClassSections_TenantId_ClassSectionId",
                        columns: x => new { x.TenantId, x.ClassSectionId },
                        principalSchema: "giddyedu",
                        principalTable: "ClassSections",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassSubjects_Subjects_TenantId_SubjectId",
                        columns: x => new { x.TenantId, x.SubjectId },
                        principalSchema: "giddyedu",
                        principalTable: "Subjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_TenantId_AcademicYearId_Code",
                schema: "giddyedu",
                table: "AcademicTerms",
                columns: new[] { "TenantId", "AcademicYearId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_TenantId_AcademicYearId_Sequence",
                schema: "giddyedu",
                table: "AcademicTerms",
                columns: new[] { "TenantId", "AcademicYearId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_TenantId",
                schema: "giddyedu",
                table: "AcademicYears",
                column: "TenantId",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_TenantId_Name",
                schema: "giddyedu",
                table: "AcademicYears",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassLevels_TenantId_Code",
                schema: "giddyedu",
                table: "ClassLevels",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassLevels_TenantId_EducationStageId",
                schema: "giddyedu",
                table: "ClassLevels",
                columns: new[] { "TenantId", "EducationStageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSections_TenantId_AcademicYearId",
                schema: "giddyedu",
                table: "ClassSections",
                columns: new[] { "TenantId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSections_TenantId_CampusId_AcademicYearId_Code",
                schema: "giddyedu",
                table: "ClassSections",
                columns: new[] { "TenantId", "CampusId", "AcademicYearId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSections_TenantId_ClassLevelId",
                schema: "giddyedu",
                table: "ClassSections",
                columns: new[] { "TenantId", "ClassLevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjects_TenantId_SubjectId",
                schema: "giddyedu",
                table: "ClassSubjects",
                columns: new[] { "TenantId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId_Code",
                schema: "giddyedu",
                table: "Departments",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EducationStages_TenantId_Code",
                schema: "giddyedu",
                table: "EducationStages",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_TenantId_Code",
                schema: "giddyedu",
                table: "Subjects",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_TenantId_DepartmentId",
                schema: "giddyedu",
                table: "Subjects",
                columns: new[] { "TenantId", "DepartmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademicTerms",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "ClassSubjects",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "SchoolProfiles",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "ClassSections",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "Subjects",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "AcademicYears",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "ClassLevels",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "giddyedu");

            migrationBuilder.DropTable(
                name: "EducationStages",
                schema: "giddyedu");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Campuses_TenantId_Id",
                schema: "giddyedu",
                table: "Campuses");
        }
    }
}
