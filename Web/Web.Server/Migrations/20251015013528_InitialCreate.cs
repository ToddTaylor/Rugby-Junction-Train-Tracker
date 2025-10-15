using Microsoft.EntityFrameworkCore.Migrations;
using Web.Server.Entities;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Owners",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Owners", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Railroads",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Railroads", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Beacons",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerID = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beacons", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Beacons_Owners_OwnerID",
                        column: x => x.OwnerID,
                        principalTable: "Owners",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subdivisions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RailroadID = table.Column<int>(type: "INTEGER", nullable: false),
                    DpuCapable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subdivisions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Subdivisions_Railroads_RailroadID",
                        column: x => x.RailroadID,
                        principalTable: "Railroads",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeaconRailroads",
                columns: table => new
                {
                    BeaconID = table.Column<int>(type: "INTEGER", nullable: false),
                    SubdivisionID = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    Milepost = table.Column<double>(type: "REAL", nullable: false),
                    MultipleTracks = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeaconRailroads", x => new { x.BeaconID, x.SubdivisionID });
                    table.ForeignKey(
                        name: "FK_BeaconRailroads_Beacons_BeaconID",
                        column: x => x.BeaconID,
                        principalTable: "Beacons",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BeaconRailroads_Subdivisions_SubdivisionID",
                        column: x => x.SubdivisionID,
                        principalTable: "Subdivisions",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapPins",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BeaconID = table.Column<int>(type: "INTEGER", nullable: false),
                    BeaconName = table.Column<string>(type: "TEXT", nullable: false),
                    SubdivisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", nullable: true),
                    DpuTrainID = table.Column<int>(type: "INTEGER", nullable: true),
                    Moving = table.Column<bool>(type: "INTEGER", nullable: true),
                    BeaconRailroadBeaconID = table.Column<int>(type: "INTEGER", nullable: true),
                    BeaconRailroadSubdivisionID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapPins", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MapPins_BeaconRailroads_BeaconID_SubdivisionId",
                        columns: x => new { x.BeaconID, x.SubdivisionId },
                        principalTable: "BeaconRailroads",
                        principalColumns: new[] { "BeaconID", "SubdivisionID" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MapPins_BeaconRailroads_BeaconRailroadBeaconID_BeaconRailroadSubdivisionID",
                        columns: x => new { x.BeaconRailroadBeaconID, x.BeaconRailroadSubdivisionID },
                        principalTable: "BeaconRailroads",
                        principalColumns: new[] { "BeaconID", "SubdivisionID" });
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AddressID = table.Column<int>(type: "INTEGER", nullable: false),
                    MapPinID = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Addresses_MapPins_MapPinID",
                        column: x => x.MapPinID,
                        principalTable: "MapPins",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Telemetries",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BeaconID = table.Column<int>(type: "INTEGER", nullable: false),
                    AddressID = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainID = table.Column<int>(type: "INTEGER", nullable: true),
                    MapPinID = table.Column<int>(type: "INTEGER", nullable: true),
                    Moving = table.Column<bool>(type: "INTEGER", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Telemetries", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Telemetries_Beacons_BeaconID",
                        column: x => x.BeaconID,
                        principalTable: "Beacons",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Telemetries_MapPins_MapPinID",
                        column: x => x.MapPinID,
                        principalTable: "MapPins",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_MapPinID",
                table: "Addresses",
                column: "MapPinID");

            migrationBuilder.CreateIndex(
                name: "IX_BeaconRailroads_SubdivisionID",
                table: "BeaconRailroads",
                column: "SubdivisionID");

            migrationBuilder.CreateIndex(
                name: "IX_Beacons_OwnerID",
                table: "Beacons",
                column: "OwnerID");

            migrationBuilder.CreateIndex(
                name: "IX_MapPins_BeaconID_SubdivisionId",
                table: "MapPins",
                columns: new[] { "BeaconID", "SubdivisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_MapPins_BeaconRailroadBeaconID_BeaconRailroadSubdivisionID",
                table: "MapPins",
                columns: new[] { "BeaconRailroadBeaconID", "BeaconRailroadSubdivisionID" });

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_RailroadID",
                table: "Subdivisions",
                column: "RailroadID");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_BeaconID",
                table: "Telemetries",
                column: "BeaconID");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_MapPinID",
                table: "Telemetries",
                column: "MapPinID");

            InitialCreateHelpers.InsertSeedData(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "Telemetries");

            migrationBuilder.DropTable(
                name: "MapPins");

            migrationBuilder.DropTable(
                name: "BeaconRailroads");

            migrationBuilder.DropTable(
                name: "Beacons");

            migrationBuilder.DropTable(
                name: "Subdivisions");

            migrationBuilder.DropTable(
                name: "Owners");

            migrationBuilder.DropTable(
                name: "Railroads");
        }
    }
}
