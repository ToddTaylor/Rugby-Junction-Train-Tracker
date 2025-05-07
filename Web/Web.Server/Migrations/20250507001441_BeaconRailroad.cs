using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class BeaconRailroad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeaconRailroad");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Beacons");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Beacons");

            migrationBuilder.CreateTable(
                name: "BeaconRailroads",
                columns: table => new
                {
                    BeaconID = table.Column<int>(type: "INTEGER", nullable: false),
                    RailroadID = table.Column<int>(type: "INTEGER", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeaconRailroads", x => new { x.BeaconID, x.RailroadID });
                    table.ForeignKey(
                        name: "FK_BeaconRailroads_Beacons_BeaconID",
                        column: x => x.BeaconID,
                        principalTable: "Beacons",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BeaconRailroads_Railroads_RailroadID",
                        column: x => x.RailroadID,
                        principalTable: "Railroads",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeaconRailroads_RailroadID",
                table: "BeaconRailroads",
                column: "RailroadID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeaconRailroads");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Beacons",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Beacons",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "BeaconRailroad",
                columns: table => new
                {
                    BeaconsID = table.Column<int>(type: "INTEGER", nullable: false),
                    RailroadsID = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeaconRailroad", x => new { x.BeaconsID, x.RailroadsID });
                    table.ForeignKey(
                        name: "FK_BeaconRailroad_Beacons_BeaconsID",
                        column: x => x.BeaconsID,
                        principalTable: "Beacons",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BeaconRailroad_Railroads_RailroadsID",
                        column: x => x.RailroadsID,
                        principalTable: "Railroads",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeaconRailroad_RailroadsID",
                table: "BeaconRailroad",
                column: "RailroadsID");
        }
    }
}
