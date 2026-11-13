using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationOwnInvoiceRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "bill_to_location_id",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_name",
                table: "customer_locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vat_id",
                table: "customer_locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_bill_to_location_id",
                table: "invoices",
                column: "bill_to_location_id");

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_customer_locations_bill_to_location_id",
                table: "invoices",
                column: "bill_to_location_id",
                principalTable: "customer_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoices_customer_locations_bill_to_location_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_bill_to_location_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "bill_to_location_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "company_name",
                table: "customer_locations");

            migrationBuilder.DropColumn(
                name: "vat_id",
                table: "customer_locations");
        }
    }
}
