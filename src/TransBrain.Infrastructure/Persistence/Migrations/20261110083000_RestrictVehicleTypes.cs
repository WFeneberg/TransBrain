using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Vehicle types are now only Tractor ("Zugmaschine") and SemiTrailer ("Hänger (Sattel)").
    /// The retired RigidTruck and Van are both motorised, so they become Tractor; the original
    /// value cannot be restored on the way down.
    /// </summary>
    public partial class RestrictVehicleTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE vehicles SET "Type" = 'Tractor' WHERE "Type" IN ('RigidTruck', 'Van');""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
