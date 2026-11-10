using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Aligns the seeded vehicle classes with the legacy vehicle screen: Sonstige, Tank, Kühler, Plane,
    /// Kipper, Leihfahrzeug. Retired classes still used by a vehicle are only deactivated, so those
    /// vehicles keep resolving; unused ones are deleted. Entries a user has edited are left alone.
    /// </summary>
    public partial class AlignVehicleClassesWithLegacy : Migration
    {
        private const string RetiredIds =
            "'0199a6c0-0000-7000-8000-000000000002', '0199a6c0-0000-7000-8000-000000000005', " +
            "'0199a6c0-0000-7000-8000-000000000007', '0199a6c0-0000-7000-8000-000000000008', " +
            "'0199a6c0-0000-7000-8000-000000000009', '0199a6c0-0000-7000-8000-00000000000a'";

        private const string UsedCodes =
            """SELECT "VehicleClass" FROM vehicles WHERE "VehicleClass" IS NOT NULL""";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""UPDATE vehicle_catalog_entries SET "IsActive" = false WHERE "Id" IN ({RetiredIds}) AND "Code" IN ({UsedCodes});""");
            migrationBuilder.Sql(
                $"""DELETE FROM vehicle_catalog_entries WHERE "Id" IN ({RetiredIds}) AND "Code" NOT IN ({UsedCodes});""");
            migrationBuilder.Sql(
                """UPDATE vehicle_catalog_entries SET "Name" = 'Kühler' WHERE "Id" = '0199a6c0-0000-7000-8000-000000000003' AND "Name" = 'Kühlkoffer';""");
            migrationBuilder.Sql(
                """
                INSERT INTO vehicle_catalog_entries ("Id", "Code", "IsActive", "Kind", "Name")
                VALUES ('0199a6c0-0000-7000-8000-00000000000c', 'Rental', true, 'VehicleClass', 'Leihfahrzeug')
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""DELETE FROM vehicle_catalog_entries WHERE "Id" = '0199a6c0-0000-7000-8000-00000000000c' AND "Code" NOT IN ({UsedCodes});""");
            migrationBuilder.Sql(
                """UPDATE vehicle_catalog_entries SET "Name" = 'Kühlkoffer' WHERE "Id" = '0199a6c0-0000-7000-8000-000000000003' AND "Name" = 'Kühler';""");
            migrationBuilder.Sql(
                """
                INSERT INTO vehicle_catalog_entries ("Id", "Code", "IsActive", "Kind", "Name") VALUES
                    ('0199a6c0-0000-7000-8000-000000000002', 'Box', true, 'VehicleClass', 'Koffer'),
                    ('0199a6c0-0000-7000-8000-000000000005', 'Silo', true, 'VehicleClass', 'Silo'),
                    ('0199a6c0-0000-7000-8000-000000000007', 'Flatbed', true, 'VehicleClass', 'Pritsche'),
                    ('0199a6c0-0000-7000-8000-000000000008', 'Container', true, 'VehicleClass', 'Container'),
                    ('0199a6c0-0000-7000-8000-000000000009', 'CarTransporter', true, 'VehicleClass', 'Autotransporter'),
                    ('0199a6c0-0000-7000-8000-00000000000a', 'LowLoader', true, 'VehicleClass', 'Tieflader')
                ON CONFLICT ("Id") DO UPDATE SET "IsActive" = true;
                """);
        }
    }
}