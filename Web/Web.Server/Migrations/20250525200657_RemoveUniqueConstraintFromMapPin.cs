using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueConstraintFromMapPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MapPins_BeaconRailroads_BeaconID_RailroadID",
                table: "MapPins");

            migrationBuilder.DropIndex(
                name: "IX_MapPins_BeaconID_RailroadID",
                table: "MapPins");

            migrationBuilder.AddColumn<int>(
                name: "BeaconRailroadBeaconID",
                table: "MapPins",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BeaconRailroadRailroadID",
                table: "MapPins",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapPins_BeaconID_RailroadID",
                table: "MapPins",
                columns: new[] { "BeaconID", "RailroadID" });

            migrationBuilder.CreateIndex(
                name: "IX_MapPins_BeaconRailroadBeaconID_BeaconRailroadRailroadID",
                table: "MapPins",
                columns: new[] { "BeaconRailroadBeaconID", "BeaconRailroadRailroadID" });

            migrationBuilder.AddForeignKey(
                name: "FK_MapPins_BeaconRailroads_BeaconID_RailroadID",
                table: "MapPins",
                columns: new[] { "BeaconID", "RailroadID" },
                principalTable: "BeaconRailroads",
                principalColumns: new[] { "BeaconID", "RailroadID" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MapPins_BeaconRailroads_BeaconRailroadBeaconID_BeaconRailroadRailroadID",
                table: "MapPins",
                columns: new[] { "BeaconRailroadBeaconID", "BeaconRailroadRailroadID" },
                principalTable: "BeaconRailroads",
                principalColumns: new[] { "BeaconID", "RailroadID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MapPins_BeaconRailroads_BeaconID_RailroadID",
                table: "MapPins");

            migrationBuilder.DropForeignKey(
                name: "FK_MapPins_BeaconRailroads_BeaconRailroadBeaconID_BeaconRailroadRailroadID",
                table: "MapPins");

            migrationBuilder.DropIndex(
                name: "IX_MapPins_BeaconID_RailroadID",
                table: "MapPins");

            migrationBuilder.DropIndex(
                name: "IX_MapPins_BeaconRailroadBeaconID_BeaconRailroadRailroadID",
                table: "MapPins");

            migrationBuilder.DropColumn(
                name: "BeaconRailroadBeaconID",
                table: "MapPins");

            migrationBuilder.DropColumn(
                name: "BeaconRailroadRailroadID",
                table: "MapPins");

            migrationBuilder.CreateIndex(
                name: "IX_MapPins_BeaconID_RailroadID",
                table: "MapPins",
                columns: new[] { "BeaconID", "RailroadID" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MapPins_BeaconRailroads_BeaconID_RailroadID",
                table: "MapPins",
                columns: new[] { "BeaconID", "RailroadID" },
                principalTable: "BeaconRailroads",
                principalColumns: new[] { "BeaconID", "RailroadID" });
        }
    }
}
