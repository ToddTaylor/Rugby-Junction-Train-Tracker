using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class MapPinRailroad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Railroad",
                table: "MapPins",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RailroadID",
                table: "MapPins",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subdivision",
                table: "MapPins",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Railroad",
                table: "MapPins");

            migrationBuilder.DropColumn(
                name: "RailroadID",
                table: "MapPins");

            migrationBuilder.DropColumn(
                name: "Subdivision",
                table: "MapPins");
        }
    }
}
