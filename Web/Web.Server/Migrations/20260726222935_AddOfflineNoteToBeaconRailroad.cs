using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineNoteToBeaconRailroad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfflineNote",
                table: "BeaconRailroads",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfflineNote",
                table: "BeaconRailroads");
        }
    }
}
