using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsLicensePlateConfidential",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ChainTensionerCount",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDangerousGoodsEquipment",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasDoubleDeck",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasDoubleDeckBeams",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasLargeLadder",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasLiftAxle",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasPalletBasket",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasPartitionWall",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSmallLadder",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "InteriorHeightMm",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InteriorLengthMm",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InteriorWidthMm",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDangerousGoodsApproved",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LashingStrapCount",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoadBarCount",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarbleRackCount",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarbleRackMaterial",
                table: "vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PalletCount",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupportPoleCount",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TankVolumeLiters",
                table: "vehicles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChainTensionerCount",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "HasDangerousGoodsEquipment",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "HasDoubleDeck",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "HasDoubleDeckBeams",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "HasLargeLadder",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "HasLiftAxle",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "HasPalletBasket",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "HasPartitionWall",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "HasSmallLadder",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "InteriorHeightMm",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "InteriorLengthMm",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "InteriorWidthMm",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "IsDangerousGoodsApproved",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "LashingStrapCount",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "LoadBarCount",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "MarbleRackCount",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "MarbleRackMaterial",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "PalletCount",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "SupportPoleCount",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "TankVolumeLiters",
                table: "vehicles");

            migrationBuilder.AlterColumn<bool>(
                name: "IsLicensePlateConfidential",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }
    }
}
