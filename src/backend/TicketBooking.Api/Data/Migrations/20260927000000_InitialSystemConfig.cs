using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketBooking.Api.Data.Migrations;

/// <inheritdoc />
public partial class InitialSystemConfig : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "system_configs",
            columns: table => new
            {
                key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_system_configs", x => x.key);
            });

        migrationBuilder.InsertData(
            table: "system_configs",
            columns: new[] { "key", "description", "updated_at", "value" },
            values: new object[] { "App:Initialized", "Dự án Bán vé sự kiện khởi tạo thành công khung S-01", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "True" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "system_configs");
    }
}
