using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddBeaconRailroadTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Beacons_Owners_OwnerID",
                table: "Beacons");

            migrationBuilder.DropForeignKey(
                name: "FK_Railroads_Beacons_BeaconID",
                table: "Railroads");

            migrationBuilder.DropIndex(
                name: "IX_Railroads_BeaconID",
                table: "Railroads");

            migrationBuilder.DropColumn(
                name: "BeaconID",
                table: "Railroads");

            migrationBuilder.AlterColumn<int>(
                name: "OwnerID",
                table: "Beacons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_Beacons_Owners_OwnerID",
                table: "Beacons",
                column: "OwnerID",
                principalTable: "Owners",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Beacons_Owners_OwnerID",
                table: "Beacons");

            migrationBuilder.DropTable(
                name: "BeaconRailroad");

            migrationBuilder.AddColumn<int>(
                name: "BeaconID",
                table: "Railroads",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OwnerID",
                table: "Beacons",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.CreateIndex(
                name: "IX_Railroads_BeaconID",
                table: "Railroads",
                column: "BeaconID");

            migrationBuilder.AddForeignKey(
                name: "FK_Beacons_Owners_OwnerID",
                table: "Beacons",
                column: "OwnerID",
                principalTable: "Owners",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Railroads_Beacons_BeaconID",
                table: "Railroads",
                column: "BeaconID",
                principalTable: "Beacons",
                principalColumn: "ID");
        }
    }
}
