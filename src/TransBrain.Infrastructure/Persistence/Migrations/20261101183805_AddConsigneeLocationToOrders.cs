using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsigneeLocationToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConsigneeLocationId",
                table: "transport_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transport_orders_ConsigneeLocationId",
                table: "transport_orders",
                column: "ConsigneeLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_transport_orders_customer_locations_ConsigneeLocationId",
                table: "transport_orders",
                column: "ConsigneeLocationId",
                principalTable: "customer_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transport_orders_customer_locations_ConsigneeLocationId",
                table: "transport_orders");

            migrationBuilder.DropIndex(
                name: "IX_transport_orders_ConsigneeLocationId",
                table: "transport_orders");

            migrationBuilder.DropColumn(
                name: "ConsigneeLocationId",
                table: "transport_orders");
        }
    }
}
