using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddServerAlertState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BeaconServerStates",
                columns: table => new
                {
                    BeaconID = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StatusSince = table.Column<string>(type: "TEXT", nullable: false),
                    PendingStatus = table.Column<string>(type: "TEXT", nullable: true),
                    PendingSince = table.Column<string>(type: "TEXT", nullable: true),
                    AlertsMutedAt = table.Column<string>(type: "TEXT", nullable: true),
                    AlertsMutedUntil = table.Column<string>(type: "TEXT", nullable: true),
                    AlertsMuteNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AlertsMutedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeaconServerStates", x => x.BeaconID);
                    table.ForeignKey(
                        name: "FK_BeaconServerStates_Beacons_BeaconID",
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
                name: "BeaconServerStates");
        }
    }
}
