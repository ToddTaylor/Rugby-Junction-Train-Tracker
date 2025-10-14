using Microsoft.EntityFrameworkCore.Migrations;

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

            InsertSeedData(migrationBuilder);
        }

        private static void InsertSeedData(MigrationBuilder migrationBuilder)
        {
            string now = DateTime.UtcNow.ToString("O");

            // Insert railroads
            migrationBuilder.InsertData(
                table: "Railroads",
                columns: new[] { "ID", "Name", "CreatedAt", "LastUpdate" },
                values: new object[,]
                {
                    { 1, "CN", now, now },
                    { 2, "WSOR", now, now }
                }
            );

            // Insert railroad subdivisions
            migrationBuilder.InsertData(
                table: "Subdivisions",
                columns: new[] { "ID", "RailroadID", "DpuCapable", "Name", "CreatedAt", "LastUpdate" },
                values: new object[,]
                {
                    { 1, 1, true, "Waukesha", now, now },
                    { 2, 2, false, "Milwaukee", now, now },
                    { 3, 1, true, "Neenah", now, now },
                    { 4, 1, true, "Superior", now, now }
                }
            );

            // Insert owners
            migrationBuilder.InsertData(
                table: "Owners",
                columns: new[] { "ID", "FirstName", "LastName", "Email", "City", "State", "CreatedAt", "LastUpdate" },
                values: new object[,]
                {
                    { 1, "Rugby", "Junction", "rugbyjunctionwi@outlook.com", "Rugby Junction", "WI", now, now },
                    { 2, "Chris", "Stromberg", "c.k.stromberg@gmail.com", "Sussex", "WI", now, now },
                    { 3, "Thomas", "Hogan", "TheSteelHighway@gmail.com", "Marion", "IA", now, now },
                    { 4, "Brian", "Sykes", "bsykes957@bellsouth.net", "<Unknown>", "SC", now, now }
                }
            );

            // Insert beacons
            migrationBuilder.InsertData(
                table: "Beacons",
                columns: new[] { "ID", "OwnerID", "CreatedAt", "LastUpdate" },
                values: new object[,]
                {
                    { 1, 1, now, now },
                    { 2, 2, now, now },
                    { 3, 3, now, now },
                    { 4, 3, now, now },
                    { 5, 3, now, now },
                    { 6, 4, now, now },
                    { 7, 4, now, now },
                    { 8, 4, now, now },
                    { 9, 4, now, now }
                }
            );

            // Insert beacon railroads
            migrationBuilder.InsertData(
                table: "BeaconRailroads",
                columns: new[] { "BeaconID", "SubdivisionID", "Direction", "Latitude", "Longitude", "Milepost", "MultipleTracks", "CreatedAt", "LastUpdate" },
                values: new object[,]
                {
                    { 1, 1, "NorthSouth", 43.280958, -88.214682, 117.2, true, now, now },
                    { 1, 2, "NorthwestSoutheast", 43.280958, -88.213966, 0, false, now, now },
                    { 2, 1, "NorthSouth", 43.159517, -88.200492, 108.6, false, now, now },
                    { 4, 4, "All", 45.463224, -91.110779, 129, true, now, now },
                    { 5, 3, "NorthSouth", 44.171033, -88.474169, 185.3, true, now, now },
                    { 6, 4, "NorthSouth", 45.993272, -91.611853, 401.2, false, now, now },
                    { 7, 4, "NorthSouth", 44.947213, -90.559989, 308, false, now, now },
                    { 8, 4, "NorthSouth", 46.244620, -91.795016, 421, false, now, now },
                    { 9, 4, "NorthSouth", 45.854653, -91.548329, 389, true, now, now }
                }
            );
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
