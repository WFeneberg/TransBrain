using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        // Every row that existed before multi-tenancy belongs to this tenant
        // (TenantDefaults.DefaultTenantId); the AddColumn defaults above back-fill it.
        private const string DefaultTenantId = "0199c0de-0000-7000-8000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vehicles_LicensePlate",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_catalog_entries_Kind_Code",
                table: "vehicle_catalog_entries");

            migrationBuilder.DropIndex(
                name: "IX_transport_orders_order_number",
                table: "transport_orders");

            migrationBuilder.DropIndex(
                name: "IX_invoices_invoice_number",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_geo_overrides_address_key",
                table: "geo_overrides");

            migrationBuilder.DropIndex(
                name: "IX_drivers_ExternalUserId",
                table: "drivers");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "vehicles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "vehicle_documents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "vehicle_catalog_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "transport_orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "tours",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "geo_overrides",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "drivers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "customers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "customer_locations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0199c0de-0000-7000-8000-000000000001"));

            // The AddColumn defaults above exist purely to back-fill pre-tenancy rows with the
            // default tenant. They must not stay on as column defaults: every insert goes through
            // the DbContext, which stamps the caller's tenant explicitly, and a lingering default
            // would let a future raw insert silently file rows under the default tenant instead
            // of failing loudly.
            migrationBuilder.Sql($"""
                ALTER TABLE vehicles ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE vehicle_documents ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE vehicle_catalog_entries ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE transport_orders ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE tours ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE invoices ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE geo_overrides ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE drivers ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE customers ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE customer_locations ALTER COLUMN tenant_id DROP DEFAULT;
                """);

            // No EF entity backs the two sequence tables (see SequentialOrderNumberGenerator);
            // reshaped by hand: each tenant draws order and invoice numbers from its own
            // per-year sequence, so the year-only primary keys become (tenant_id, year).
            migrationBuilder.Sql($"""
                ALTER TABLE order_number_sequences
                    ADD COLUMN tenant_id uuid NOT NULL DEFAULT '{DefaultTenantId}';
                ALTER TABLE order_number_sequences ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE order_number_sequences DROP CONSTRAINT order_number_sequences_pkey;
                ALTER TABLE order_number_sequences ADD PRIMARY KEY (tenant_id, year);

                ALTER TABLE invoice_number_sequences
                    ADD COLUMN tenant_id uuid NOT NULL DEFAULT '{DefaultTenantId}';
                ALTER TABLE invoice_number_sequences ALTER COLUMN tenant_id DROP DEFAULT;
                ALTER TABLE invoice_number_sequences DROP CONSTRAINT invoice_number_sequences_pkey;
                ALTER TABLE invoice_number_sequences ADD PRIMARY KEY (tenant_id, year);
                """);

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000001"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000003"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000004"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000006"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-00000000000b"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-00000000000c"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000101"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000102"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000103"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000104"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000105"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000106"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000107"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000201"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000202"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000203"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000204"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000205"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000206"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000301"),
                column: "tenant_id",
                value: new Guid("0199c0de-0000-7000-8000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_tenant_id",
                table: "vehicles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_tenant_id_LicensePlate",
                table: "vehicles",
                columns: new[] { "tenant_id", "LicensePlate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_documents_tenant_id",
                table: "vehicle_documents",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_catalog_entries_tenant_id",
                table: "vehicle_catalog_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_catalog_entries_tenant_id_Kind_Code",
                table: "vehicle_catalog_entries",
                columns: new[] { "tenant_id", "Kind", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transport_orders_tenant_id",
                table: "transport_orders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_transport_orders_tenant_id_order_number",
                table: "transport_orders",
                columns: new[] { "tenant_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tours_tenant_id",
                table: "tours",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_id",
                table: "invoices",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_id_invoice_number",
                table: "invoices",
                columns: new[] { "tenant_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_geo_overrides_tenant_id",
                table: "geo_overrides",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_geo_overrides_tenant_id_address_key",
                table: "geo_overrides",
                columns: new[] { "tenant_id", "address_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_tenant_id",
                table: "drivers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_drivers_tenant_id_ExternalUserId",
                table: "drivers",
                columns: new[] { "tenant_id", "ExternalUserId" },
                unique: true,
                filter: "\"ExternalUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id",
                table: "customers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_locations_tenant_id",
                table: "customer_locations",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back to single-tenant keeps exactly the default tenant's sequences: the
            // year-only primary key cannot hold more than one tenant's counters, and the rest of
            // this Down (global unique indexes) assumes only the default tenant's data remains.
            migrationBuilder.Sql($"""
                DELETE FROM order_number_sequences WHERE tenant_id <> '{DefaultTenantId}';
                ALTER TABLE order_number_sequences DROP CONSTRAINT order_number_sequences_pkey;
                ALTER TABLE order_number_sequences DROP COLUMN tenant_id;
                ALTER TABLE order_number_sequences ADD PRIMARY KEY (year);

                DELETE FROM invoice_number_sequences WHERE tenant_id <> '{DefaultTenantId}';
                ALTER TABLE invoice_number_sequences DROP CONSTRAINT invoice_number_sequences_pkey;
                ALTER TABLE invoice_number_sequences DROP COLUMN tenant_id;
                ALTER TABLE invoice_number_sequences ADD PRIMARY KEY (year);
                """);

            migrationBuilder.DropIndex(
                name: "IX_vehicles_tenant_id",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "IX_vehicles_tenant_id_LicensePlate",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_documents_tenant_id",
                table: "vehicle_documents");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_catalog_entries_tenant_id",
                table: "vehicle_catalog_entries");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_catalog_entries_tenant_id_Kind_Code",
                table: "vehicle_catalog_entries");

            migrationBuilder.DropIndex(
                name: "IX_transport_orders_tenant_id",
                table: "transport_orders");

            migrationBuilder.DropIndex(
                name: "IX_transport_orders_tenant_id_order_number",
                table: "transport_orders");

            migrationBuilder.DropIndex(
                name: "IX_tours_tenant_id",
                table: "tours");

            migrationBuilder.DropIndex(
                name: "IX_invoices_tenant_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_tenant_id_invoice_number",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_geo_overrides_tenant_id",
                table: "geo_overrides");

            migrationBuilder.DropIndex(
                name: "IX_geo_overrides_tenant_id_address_key",
                table: "geo_overrides");

            migrationBuilder.DropIndex(
                name: "IX_drivers_tenant_id",
                table: "drivers");

            migrationBuilder.DropIndex(
                name: "IX_drivers_tenant_id_ExternalUserId",
                table: "drivers");

            migrationBuilder.DropIndex(
                name: "IX_customers_tenant_id",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customer_locations_tenant_id",
                table: "customer_locations");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "vehicle_documents");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "vehicle_catalog_entries");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "transport_orders");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "tours");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "geo_overrides");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "customer_locations");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_LicensePlate",
                table: "vehicles",
                column: "LicensePlate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_catalog_entries_Kind_Code",
                table: "vehicle_catalog_entries",
                columns: new[] { "Kind", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transport_orders_order_number",
                table: "transport_orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_invoice_number",
                table: "invoices",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_geo_overrides_address_key",
                table: "geo_overrides",
                column: "address_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_ExternalUserId",
                table: "drivers",
                column: "ExternalUserId",
                unique: true,
                filter: "\"ExternalUserId\" IS NOT NULL");
        }
    }
}
