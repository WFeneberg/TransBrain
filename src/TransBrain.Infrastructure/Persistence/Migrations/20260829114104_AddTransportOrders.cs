using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transport_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, collation: "C"),
                    consignor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consignor_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consignor_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    consignor_city = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consignor_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    consignee_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consignee_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consignee_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    consignee_city = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consignee_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    cargo_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    cargo_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    cargo_load_meters = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    pickup_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pickup_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivery_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivery_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transport_orders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transport_orders_order_number",
                table: "transport_orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transport_orders_pickup_from",
                table: "transport_orders",
                column: "pickup_from");

            // No EF entity backs this table: the generator increments it with a single atomic
            // UPDATE ... RETURNING statement, which is the whole point of the design.
            migrationBuilder.Sql("""
                CREATE TABLE order_number_sequences (
                    year integer NOT NULL PRIMARY KEY,
                    last_number integer NOT NULL
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE order_number_sequences;");

            migrationBuilder.DropTable(
                name: "transport_orders");
        }
    }
}
