using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAdditionalData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "customers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerClass",
                table: "customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeTollFromInvoice",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FoundedOn",
                table: "customers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReimburseToll",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPalletCheck",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SalariedEmployeeCount",
                table: "customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkerCount",
                table: "customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "secondary_city",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "secondary_country",
                table: "customers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "secondary_name",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "secondary_postal_code",
                table: "customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "secondary_street",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.InsertData(
                table: "vehicle_catalog_entries",
                columns: new[] { "Id", "Code", "IsActive", "Kind", "Name" },
                values: new object[] { new Guid("0199a6c0-0000-7000-8000-000000000301"), "HuterVilsAll", true, "CustomerClass", "Huter Vils alle Gattungen" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000301"));

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CustomerClass",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "ExcludeTollFromInvoice",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "FoundedOn",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "ReimburseToll",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "RequiresPalletCheck",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "SalariedEmployeeCount",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "WorkerCount",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "secondary_city",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "secondary_country",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "secondary_name",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "secondary_postal_code",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "secondary_street",
                table: "customers");
        }
    }
}
