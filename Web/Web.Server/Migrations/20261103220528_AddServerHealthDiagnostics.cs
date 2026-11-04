using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddServerHealthDiagnostics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpectedSdrSharpCount",
                table: "Beacons",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedSoftDpuCount",
                table: "Beacons",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedSoftEotCount",
                table: "Beacons",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BeaconDiagnostics",
                columns: table => new
                {
                    BeaconID = table.Column<int>(type: "INTEGER", nullable: false),
                    CollectedAt = table.Column<string>(type: "TEXT", nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", nullable: true),
                    ClientVersion = table.Column<string>(type: "TEXT", nullable: true),
                    OsVersion = table.Column<string>(type: "TEXT", nullable: true),
                    LastBootAt = table.Column<string>(type: "TEXT", nullable: true),
                    LogDriveFreeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    HealthPingIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LastTelemetrySentAt = table.Column<string>(type: "TEXT", nullable: true),
                    TelemetrySendFailuresLastHour = table.Column<int>(type: "INTEGER", nullable: false),
                    Programs = table.Column<string>(type: "TEXT", nullable: false),
                    Logs = table.Column<string>(type: "TEXT", nullable: false),
                    StartupTasks = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeaconDiagnostics", x => x.BeaconID);
                    table.ForeignKey(
                        name: "FK_BeaconDiagnostics_Beacons_BeaconID",
                        column: x => x.BeaconID,
                        principalTable: "Beacons",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeaconDiagnostics");

            migrationBuilder.DropColumn(
                name: "ExpectedSdrSharpCount",
                table: "Beacons");

            migrationBuilder.DropColumn(
                name: "ExpectedSoftDpuCount",
                table: "Beacons");

            migrationBuilder.DropColumn(
                name: "ExpectedSoftEotCount",
                table: "Beacons");
        }
    }
}
