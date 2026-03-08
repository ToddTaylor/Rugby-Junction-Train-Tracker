using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetryBrakePressure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BrakePipePressure",
                table: "Telemetries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrakePipePressure",
                table: "Telemetries");
        }
    }
}
