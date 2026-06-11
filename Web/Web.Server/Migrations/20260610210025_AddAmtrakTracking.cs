using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAmtrakTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AmtrakPollingConfigurations",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PollIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmtrakPollingConfigurations", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AmtrakTrackedTrains",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrainNumber = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmtrakTrackedTrains", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PassengerMapPins",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RouteName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TrainNum = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    TrainId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Heading = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    Velocity = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    IsStale = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassengerMapPins", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmtrakTrackedTrains_TrainNumber",
                table: "AmtrakTrackedTrains",
                column: "TrainNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PassengerMapPins_TrainId",
                table: "PassengerMapPins",
                column: "TrainId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmtrakPollingConfigurations");

            migrationBuilder.DropTable(
                name: "AmtrakTrackedTrains");

            migrationBuilder.DropTable(
                name: "PassengerMapPins");
        }
    }
}
