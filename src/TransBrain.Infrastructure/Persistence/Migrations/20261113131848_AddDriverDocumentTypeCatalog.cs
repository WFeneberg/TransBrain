using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverDocumentTypeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "name",
                table: "driver_special_documents",
                newName: "type");

            migrationBuilder.InsertData(
                table: "vehicle_catalog_entries",
                columns: new[] { "Id", "Code", "IsActive", "Kind", "Name", "tenant_id" },
                values: new object[,]
                {
                    { new Guid("0199a6c0-0000-7000-8000-000000000501"), "DangerousGoods", true, "DriverDocumentType", "Gefahrengutschein", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000502"), "MedicalOfficer", true, "DriverDocumentType", "Amtsarzt", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000503"), "TuvAssessment", true, "DriverDocumentType", "TÜV - Begutachtung", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000504"), "B3Certificate", true, "DriverDocumentType", "B-3 - Bescheinigung", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000505"), "SpecialApproval", true, "DriverDocumentType", "besondere Zulassung", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000506"), "SpecialPermit", true, "DriverDocumentType", "besondere Genehmigung", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000507"), "BoilerPressure", true, "DriverDocumentType", "Kesseldruckbescheinigung", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000508"), "ServiceInterval", true, "DriverDocumentType", "Service - Intervall", new Guid("0199c0de-0000-7000-8000-000000000001") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000501"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000502"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000503"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000504"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000505"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000506"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000507"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000508"));

            migrationBuilder.RenameColumn(
                name: "type",
                table: "driver_special_documents",
                newName: "name");
        }
    }
}
