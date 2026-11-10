using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds the carrier groups of the legacy vehicle screen: Sonstige, Huter Vils, Huter Füssen, Fixe
    /// Frächter, Regelmäßig beschäftigte Frächter, Leihfahrzeuge. A group someone already created with
    /// the same code (in any letter case) is kept and its seed skipped.
    /// </summary>
    public partial class SeedLegacyCarrierGroups : Migration
    {
        private const string SeededIds =
            "'0199a6c0-0000-7000-8000-000000000201', '0199a6c0-0000-7000-8000-000000000202', " +
            "'0199a6c0-0000-7000-8000-000000000203', '0199a6c0-0000-7000-8000-000000000204', " +
            "'0199a6c0-0000-7000-8000-000000000205', '0199a6c0-0000-7000-8000-000000000206'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO vehicle_catalog_entries ("Id", "Code", "IsActive", "Kind", "Name")
                SELECT seed."Id"::uuid, seed."Code", true, 'CarrierGroup', seed."Name"
                FROM (VALUES
                    ('0199a6c0-0000-7000-8000-000000000201', 'Other', 'Sonstige'),
                    ('0199a6c0-0000-7000-8000-000000000202', 'HuterVils', 'Huter Vils'),
                    ('0199a6c0-0000-7000-8000-000000000203', 'HuterFuessen', 'Huter Füssen'),
                    ('0199a6c0-0000-7000-8000-000000000204', 'Fixed', 'Fixe Frächter'),
                    ('0199a6c0-0000-7000-8000-000000000205', 'Regular', 'Regelmäßig beschäftigte Frächter'),
                    ('0199a6c0-0000-7000-8000-000000000206', 'Rental', 'Leihfahrzeuge')
                ) AS seed ("Id", "Code", "Name")
                WHERE NOT EXISTS (
                    SELECT 1 FROM vehicle_catalog_entries existing
                    WHERE existing."Kind" = 'CarrierGroup' AND lower(existing."Code") = lower(seed."Code"))
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                DELETE FROM vehicle_catalog_entries
                WHERE "Id" IN ({SeededIds})
                  AND "Code" NOT IN (SELECT "CarrierGroup" FROM vehicles WHERE "CarrierGroup" IS NOT NULL);
                """);
        }
    }
}