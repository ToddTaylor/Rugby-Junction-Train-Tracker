using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class MoveTrainIdToAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DpuTrainID",
                table: "MapPins");

            migrationBuilder.AddColumn<int>(
                name: "DpuTrainID",
                table: "Addresses",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DpuTrainID",
                table: "Addresses");

            migrationBuilder.AddColumn<int>(
                name: "DpuTrainID",
                table: "MapPins",
                type: "INTEGER",
                nullable: true);
        }
    }
}
