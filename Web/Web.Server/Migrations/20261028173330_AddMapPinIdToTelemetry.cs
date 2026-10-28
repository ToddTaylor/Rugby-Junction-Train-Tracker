using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMapPinIdToTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MapPinID",
                table: "Telemetries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_MapPinID_CreatedAt",
                table: "Telemetries",
                columns: new[] { "MapPinID", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Telemetries_MapPinID_CreatedAt",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "MapPinID",
                table: "Telemetries");
        }
    }
}
