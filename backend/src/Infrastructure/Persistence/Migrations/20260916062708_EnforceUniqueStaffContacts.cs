using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueStaffContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_TenantId_Phone",
                schema: "giddyedu",
                table: "StaffProfiles",
                columns: new[] { "TenantId", "Phone" },
                unique: true,
                filter: "\"Phone\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_TenantId_WorkEmail",
                schema: "giddyedu",
                table: "StaffProfiles",
                columns: new[] { "TenantId", "WorkEmail" },
                unique: true,
                filter: "\"WorkEmail\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffProfiles_TenantId_Phone",
                schema: "giddyedu",
                table: "StaffProfiles");

            migrationBuilder.DropIndex(
                name: "IX_StaffProfiles_TenantId_WorkEmail",
                schema: "giddyedu",
                table: "StaffProfiles");
        }
    }
}
