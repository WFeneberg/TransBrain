using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTollsAndCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfirmationNotes",
                table: "transport_orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "transport_orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresDoubleDeck",
                table: "transport_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPartitionWall",
                table: "transport_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "order_additional_charges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_additional_charges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_additional_charges_transport_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "transport_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_toll_charges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TollCategory = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmptyKm = table.Column<int>(type: "integer", nullable: false),
                    LoadedKm = table.Column<int>(type: "integer", nullable: false),
                    FactorPerKm = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_toll_charges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_toll_charges_transport_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "transport_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "vehicle_catalog_entries",
                columns: new[] { "Id", "Code", "IsActive", "Kind", "Name", "tenant_id" },
                values: new object[,]
                {
                    { new Guid("0199a6c0-0000-7000-8000-000000000601"), "A-0300", true, "TollCategory", "A 0,300", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000602"), "A-0310", true, "TollCategory", "A 0,310", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000603"), "A1", true, "TollCategory", "A1", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000604"), "A2", true, "TollCategory", "A2", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000605"), "A-Tank", true, "TollCategory", "A-Tank", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000606"), "CH-055", true, "TollCategory", "CH 0,55", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000607"), "D-0124", true, "TollCategory", "D 0,124", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000608"), "D-015", true, "TollCategory", "D 0,15", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000609"), "D-Tank", true, "TollCategory", "D Tank", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-00000000060a"), "D3A-EU12", true, "TollCategory", "D3A EU 1/2", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-00000000060b"), "D3A-EU34", true, "TollCategory", "D3A EU 3/4", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-00000000060c"), "D3A-EU5", true, "TollCategory", "D3A EU 5", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-00000000060d"), "D45A-EU5", true, "TollCategory", "D4/5A EU 5", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-00000000060e"), "D45A-EU12", true, "TollCategory", "D4/5A EU 1/2", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-00000000060f"), "D45A-EU34", true, "TollCategory", "D4/5A EU 3/4", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000701"), "m", true, "Unit", "Meter", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000702"), "cm", true, "Unit", "Zentimeter", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000703"), "mm", true, "Unit", "Millimeter", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000704"), "Stk", true, "Unit", "Stück", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000705"), "Pck", true, "Unit", "Packung", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000706"), "Std", true, "Unit", "Stunde", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000707"), "min", true, "Unit", "Minute", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000708"), "Tg", true, "Unit", "Tag", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000709"), "ltr", true, "Unit", "Liter", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-00000000070a"), "to", true, "Unit", "Tonnen", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-00000000070b"), "cbm", true, "Unit", "Kubikmeter", new Guid("0199c0de-0000-7000-8000-000000000001") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_additional_charges_OrderId",
                table: "order_additional_charges",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_additional_charges_tenant_id",
                table: "order_additional_charges",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_toll_charges_OrderId",
                table: "order_toll_charges",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_toll_charges_tenant_id",
                table: "order_toll_charges",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_additional_charges");

            migrationBuilder.DropTable(
                name: "order_toll_charges");

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000601"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000602"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000603"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000604"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000605"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000606"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000607"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000608"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000609"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-00000000060a"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-00000000060b"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-00000000060c"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-00000000060d"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-00000000060e"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-00000000060f"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000701"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000702"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000703"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000704"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000705"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000706"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000707"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000708"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000709"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-00000000070a"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-00000000070b"));

            migrationBuilder.DropColumn(
                name: "ConfirmationNotes",
                table: "transport_orders");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "transport_orders");

            migrationBuilder.DropColumn(
                name: "RequiresDoubleDeck",
                table: "transport_orders");

            migrationBuilder.DropColumn(
                name: "RequiresPartitionWall",
                table: "transport_orders");
        }
    }
}
