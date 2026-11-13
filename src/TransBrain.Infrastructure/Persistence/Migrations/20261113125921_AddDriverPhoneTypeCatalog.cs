using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverPhoneTypeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "vehicle_catalog_entries",
                columns: new[] { "Id", "Code", "IsActive", "Kind", "Name", "tenant_id" },
                values: new object[,]
                {
                    { new Guid("0199a6c0-0000-7000-8000-000000000401"), "Fax", true, "PhoneType", "Fax", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000402"), "Phone", true, "PhoneType", "Telefon", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000403"), "Email", true, "PhoneType", "Email", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000404"), "CarPhone", true, "PhoneType", "Auto-Telefon", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000405"), "HomePage", true, "PhoneType", "Home-Page", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000406"), "Private", true, "PhoneType", "Privat", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000407"), "Emergency", true, "PhoneType", "Notruf", new Guid("0199c0de-0000-7000-8000-000000000001") },
                    { new Guid("0199a6c0-0000-7000-8000-000000000408"), "CompanyExtension", true, "PhoneType", "Firma (DW)", new Guid("0199c0de-0000-7000-8000-000000000001") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000401"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000402"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000403"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000404"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000405"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000406"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000407"));

            migrationBuilder.DeleteData(
                table: "vehicle_catalog_entries",
                keyColumn: "Id",
                keyValue: new Guid("0199a6c0-0000-7000-8000-000000000408"));
        }
    }
}
