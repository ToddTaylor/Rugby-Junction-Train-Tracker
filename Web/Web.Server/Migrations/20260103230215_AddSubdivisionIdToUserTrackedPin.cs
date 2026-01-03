using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSubdivisionIdToUserTrackedPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BeaconName",
                table: "UserTrackedPins");

            migrationBuilder.AddColumn<int>(
                name: "SubdivisionID",
                table: "UserTrackedPins",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubdivisionID",
                table: "UserTrackedPins");

            migrationBuilder.AddColumn<string>(
                name: "BeaconName",
                table: "UserTrackedPins",
                type: "TEXT",
                nullable: true);
        }
    }
}
