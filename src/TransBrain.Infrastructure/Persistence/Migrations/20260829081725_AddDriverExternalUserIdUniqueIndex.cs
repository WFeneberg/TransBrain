using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverExternalUserIdUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_drivers_ExternalUserId",
                table: "drivers",
                column: "ExternalUserId",
                unique: true,
                filter: "\"ExternalUserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_drivers_ExternalUserId",
                table: "drivers");
        }
    }
}
