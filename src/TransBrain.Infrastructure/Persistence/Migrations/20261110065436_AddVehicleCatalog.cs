using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle_catalog_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_catalog_entries", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "vehicle_catalog_entries",
                columns: new[] { "Id", "Code", "IsActive", "Kind", "Name" },
                values: new object[,]
                {
                    { new Guid("0199a6c0-0000-7000-8000-000000000001"), "Tarpaulin", true, "VehicleClass", "Plane" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000002"), "Box", true, "VehicleClass", "Koffer" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000003"), "Refrigerated", true, "VehicleClass", "Kühlkoffer" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000004"), "Tank", true, "VehicleClass", "Tank" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000005"), "Silo", true, "VehicleClass", "Silo" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000006"), "Tipper", true, "VehicleClass", "Kipper" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000007"), "Flatbed", true, "VehicleClass", "Pritsche" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000008"), "Container", true, "VehicleClass", "Container" },
                    { new Guid("0199a6c0-0000-7000-8000-000000000009"), "CarTransporter", true, "VehicleClass", "Autotransporter" },
                    { new Guid("0199a6c0-0000-7000-8000-00000000000a"), "LowLoader", true, "VehicleClass", "Tieflader" },
                    { new Guid("0199a6c0-0000-7000-8000-00000000000b"), "Other", true, "VehicleClass", "Sonstige" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_catalog_entries_Kind_Code",
                table: "vehicle_catalog_entries",
                columns: new[] { "Kind", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_catalog_entries");
        }
    }
}
