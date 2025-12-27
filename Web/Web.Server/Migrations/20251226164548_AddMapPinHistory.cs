using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMapPinHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MapPinHistories",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BeaconID = table.Column<int>(type: "INTEGER", nullable: false),
                    SubdivisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", nullable: true),
                    Moving = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsLocal = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddressesJson = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalMapPinID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapPinHistories", x => x.ID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapPinHistories");
        }
    }
}
