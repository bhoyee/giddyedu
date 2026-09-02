using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiddyEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1SubscriptionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO giddyedu."Plans" ("Id", "Code", "Name", "IsActive")
                VALUES ('7fc03f31-c810-49bd-891c-e6527655dcaa', 'school-core', 'School Core', TRUE)
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO giddyedu."Features" ("Id", "Key", "Name", "ValueType") VALUES
                    ('cc9e67e3-a9e1-48e0-8b83-f24d56fcc834', 'school-administration', 'School Administration', 0),
                    ('e8420c23-d865-459d-9161-ceaa4a8d406f', 'academic-structure', 'Academic Structure', 0),
                    ('be4ea911-3a91-41b0-a137-863473375f35', 'staff-management', 'Staff Management', 0),
                    ('ed92de01-db35-4eb5-9575-fb201ed585a4', 'admissions', 'Admissions', 0),
                    ('c7aed5f2-cd49-4f79-bbfd-7194aedeb393', 'student-information', 'Student Information System', 0),
                    ('6354859a-06a7-47a5-aede-c9449f3ea825', 'guardian-management', 'Parent and Guardian Management', 0)
                ON CONFLICT ("Key") DO NOTHING;

                INSERT INTO giddyedu."PlanEntitlements" ("PlanId", "FeatureId", "Enabled", "Limit")
                SELECT plan."Id", feature."Id", TRUE, NULL
                FROM giddyedu."Plans" plan CROSS JOIN giddyedu."Features" feature
                WHERE plan."Code" = 'school-core'
                  AND feature."Key" IN ('school-administration','academic-structure','staff-management','admissions','student-information','guardian-management')
                ON CONFLICT DO NOTHING;

                INSERT INTO giddyedu."TenantSubscriptions" ("Id", "TenantId", "PlanId", "StartsAtUtc", "EndsAtUtc", "IsActive")
                SELECT gen_random_uuid(), tenant."Id", plan."Id", NOW(), NULL, TRUE
                FROM giddyedu."Tenants" tenant CROSS JOIN giddyedu."Plans" plan
                WHERE plan."Code" = 'school-core'
                  AND NOT EXISTS (SELECT 1 FROM giddyedu."TenantSubscriptions" subscription WHERE subscription."TenantId" = tenant."Id" AND subscription."IsActive" = TRUE);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM giddyedu."TenantSubscriptions" WHERE "PlanId" = '7fc03f31-c810-49bd-891c-e6527655dcaa';
                DELETE FROM giddyedu."PlanEntitlements" WHERE "PlanId" = '7fc03f31-c810-49bd-891c-e6527655dcaa';
                DELETE FROM giddyedu."Features" WHERE "Id" IN ('cc9e67e3-a9e1-48e0-8b83-f24d56fcc834','e8420c23-d865-459d-9161-ceaa4a8d406f','be4ea911-3a91-41b0-a137-863473375f35','ed92de01-db35-4eb5-9575-fb201ed585a4','c7aed5f2-cd49-4f79-bbfd-7194aedeb393','6354859a-06a7-47a5-aede-c9449f3ea825');
                DELETE FROM giddyedu."Plans" WHERE "Id" = '7fc03f31-c810-49bd-891c-e6527655dcaa';
                """);
        }
    }
}
