using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverSocialInsuranceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "drivers",
                type: "character varying(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankCode",
                table: "drivers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "drivers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bic",
                table: "drivers",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EntryDate",
                table: "drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyDoctor",
                table: "drivers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyDoctorPhone",
                table: "drivers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HealthInsurance",
                table: "drivers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                table: "drivers",
                type: "character varying(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "drivers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfBirth",
                table: "drivers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialInsuranceNumber",
                table: "drivers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "BankCode",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "Bic",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "EntryDate",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "FamilyDoctor",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "FamilyDoctorPhone",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "HealthInsurance",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "Iban",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "PlaceOfBirth",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "SocialInsuranceNumber",
                table: "drivers");
        }
    }
}
