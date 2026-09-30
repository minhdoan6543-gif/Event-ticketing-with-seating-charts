using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EventTicketing.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatsAndSeatCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "seat_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    performance_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seat_categories", x => x.id);
                    table.UniqueConstraint("AK_seat_categories_performance_id_id", x => new { x.performance_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "seats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    performance_id = table.Column<int>(type: "integer", nullable: false),
                    seat_category_id = table.Column<int>(type: "integer", nullable: false),
                    row = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    held_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    held_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seats", x => x.id);
                    table.CheckConstraint("CK_seats_hold_fields", "status <> 'HELD' OR (held_by_user_id IS NOT NULL AND held_until IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_seats_seat_categories_performance_id_seat_category_id",
                        columns: x => new { x.performance_id, x.seat_category_id },
                        principalTable: "seat_categories",
                        principalColumns: new[] { "performance_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seat_categories_performance_id_normalized_name",
                table: "seat_categories",
                columns: new[] { "performance_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seats_performance_id_row_number",
                table: "seats",
                columns: new[] { "performance_id", "row", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seats_performance_id_seat_category_id",
                table: "seats",
                columns: new[] { "performance_id", "seat_category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_seats_performance_id_status_held_until",
                table: "seats",
                columns: new[] { "performance_id", "status", "held_until" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seats");

            migrationBuilder.DropTable(
                name: "seat_categories");
        }
    }
}
