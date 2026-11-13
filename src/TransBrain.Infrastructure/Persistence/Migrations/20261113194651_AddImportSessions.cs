using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TargetEntity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TableName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    ImportedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_session_data",
                columns: table => new
                {
                    ImportSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_session_data", x => x.ImportSessionId);
                    table.ForeignKey(
                        name: "FK_import_session_data_import_sessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "import_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_session_data_tenant_id",
                table: "import_session_data",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_import_sessions_CreatedAtUtc",
                table: "import_sessions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_import_sessions_tenant_id",
                table: "import_sessions",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_session_data");

            migrationBuilder.DropTable(
                name: "import_sessions");
        }
    }
}
