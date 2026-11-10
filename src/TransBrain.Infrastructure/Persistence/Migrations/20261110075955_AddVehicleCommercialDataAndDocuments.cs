using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleCommercialDataAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostCenter",
                table: "vehicles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSheet",
                table: "vehicles",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FirstRegisteredOn",
                table: "vehicles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseOdometerKm",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                table: "vehicles",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PurchasedOn",
                table: "vehicles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportInsurancePolicyNumbers",
                table: "vehicles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportInsurer",
                table: "vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsefulLifeKm",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsefulLifeYears",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleInsurancePolicyNumbers",
                table: "vehicles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleInsurer",
                table: "vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vehicle_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastCheckedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_documents_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "vehicle_catalog_entries",
                columns: new[] { "Id", "Code", "IsActive", "Kind", "Name" },
                values: new object[,]
                {
                    { new Guid("0199a6c0-0000-7000-8000-000000000101"), "ADR", true, "DocumentType", "ADR-Zulassungsbescheinigung" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000102"), "ATP", true, "DocumentType", "ATP-Bescheinigung" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000103"), "CEMT", true, "DocumentType", "CEMT-Genehmigung" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000104"), "Tachograph", true, "DocumentType", "Tachographenprüfung" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000105"), "UVV", true, "DocumentType", "UVV-Prüfung" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000106"), "Crane", true, "DocumentType", "Kranprüfung" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000107"), "TailLift", true, "DocumentType", "Ladebordwandprüfung" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_documents_VehicleId_DocumentType",
                table: "vehicle_documents",
                columns: new[] { "VehicleId", "DocumentType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_documents");

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000101"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000102"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000103"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000104"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000105"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000106"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000107"));

            migrationBuilder.DropColumn(
                name: "CostCenter",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "DataSheet",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "FirstRegisteredOn",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "PurchaseOdometerKm",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "PurchasedOn",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "TransportInsurancePolicyNumbers",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "TransportInsurer",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "UsefulLifeKm",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "UsefulLifeYears",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "VehicleInsurancePolicyNumbers",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "VehicleInsurer",
                table: "vehicles");
        }
    }
}
