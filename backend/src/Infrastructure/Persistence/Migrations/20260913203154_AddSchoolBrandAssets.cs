using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolBrandAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrincipalSignatureFileId",
                schema: "giddyedu",
                table: "SchoolProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_StoredFiles_TenantId_Id",
                schema: "giddyedu",
                table: "StoredFiles",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolProfiles_TenantId_LogoFileId",
                schema: "giddyedu",
                table: "SchoolProfiles",
                columns: new[] { "TenantId", "LogoFileId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolProfiles_TenantId_PrincipalSignatureFileId",
                schema: "giddyedu",
                table: "SchoolProfiles",
                columns: new[] { "TenantId", "PrincipalSignatureFileId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolProfiles_StoredFiles_TenantId_LogoFileId",
                schema: "giddyedu",
                table: "SchoolProfiles",
                columns: new[] { "TenantId", "LogoFileId" },
                principalSchema: "giddyedu",
                principalTable: "StoredFiles",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolProfiles_StoredFiles_TenantId_PrincipalSignatureFileId",
                schema: "giddyedu",
                table: "SchoolProfiles",
                columns: new[] { "TenantId", "PrincipalSignatureFileId" },
                principalSchema: "giddyedu",
                principalTable: "StoredFiles",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchoolProfiles_StoredFiles_TenantId_LogoFileId",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolProfiles_StoredFiles_TenantId_PrincipalSignatureFileId",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_StoredFiles_TenantId_Id",
                schema: "giddyedu",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_SchoolProfiles_TenantId_LogoFileId",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SchoolProfiles_TenantId_PrincipalSignatureFileId",
                schema: "giddyedu",
                table: "SchoolProfiles");

            migrationBuilder.DropColumn(
                name: "PrincipalSignatureFileId",
                schema: "giddyedu",
                table: "SchoolProfiles");
        }
    }
}
