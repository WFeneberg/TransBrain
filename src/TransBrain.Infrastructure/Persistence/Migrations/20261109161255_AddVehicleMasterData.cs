using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarrierGroup",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChassisNumber",
                table: "vehicles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurbWeightKg",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultDriverId",
                table: "vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EngineNumber",
                table: "vehicles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrossWeightKg",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeightMm",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLicensePlateConfidential",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LengthMm",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Make",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ManufacturedOn",
                table: "vehicles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxFifthWheelLoadKg",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ModifiedAt",
                table: "vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "vehicles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PowerKw",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleClass",
                table: "vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WidthMm",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_DefaultDriverId",
                table: "vehicles",
                column: "DefaultDriverId");

            migrationBuilder.AddForeignKey(
                name: "FK_vehicles_drivers_DefaultDriverId",
                table: "vehicles",
                column: "DefaultDriverId",
                principalTable: "drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vehicles_drivers_DefaultDriverId",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "IX_vehicles_DefaultDriverId",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "CarrierGroup",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "ChassisNumber",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "CurbWeightKg",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "DefaultDriverId",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "EngineNumber",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "GrossWeightKg",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "HeightMm",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "IsLicensePlateConfidential",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "LengthMm",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "Make",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "ManufacturedOn",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "MaxFifthWheelLoadKg",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "PowerKw",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "VehicleClass",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "WidthMm",
                table: "vehicles");
        }
    }
}
