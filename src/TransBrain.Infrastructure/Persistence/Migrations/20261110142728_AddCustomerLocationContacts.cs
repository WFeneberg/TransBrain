using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TransBrain.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Replaces the single contact person of a delivery location with a list of contacts. The
    /// existing contact is carried over as the first entry; a contact without a name but with a
    /// phone or e-mail gets the placeholder name "Ansprechperson".
    /// </summary>
    public partial class AddCustomerLocationContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_location_contacts",
                columns: table => new
                {
                    customer_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_location_contacts", x => new { x.customer_location_id, x.Id });
                    table.ForeignKey(
                        name: "FK_customer_location_contacts_customer_locations_customer_loca~",
                        column: x => x.customer_location_id,
                        principalTable: "customer_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO customer_location_contacts (customer_location_id, sequence, name, phone, email)
                SELECT "Id", 1, COALESCE("ContactPerson", 'Ansprechperson'), "Phone", "Email"
                FROM customer_locations
                WHERE "ContactPerson" IS NOT NULL OR "Phone" IS NOT NULL OR "Email" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "ContactPerson",
                table: "customer_locations");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "customer_locations");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "customer_locations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactPerson",
                table: "customer_locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "customer_locations",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "customer_locations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Only the first contact fits into the old single-contact columns.
            migrationBuilder.Sql("""
                UPDATE customer_locations AS l
                SET "ContactPerson" = c.name, "Phone" = c.phone, "Email" = c.email
                FROM customer_location_contacts AS c
                WHERE c.customer_location_id = l."Id"
                  AND c.sequence = (SELECT MIN(sequence) FROM customer_location_contacts WHERE customer_location_id = l."Id");
                """);

            migrationBuilder.DropTable(
                name: "customer_location_contacts");
        }
    }
}
