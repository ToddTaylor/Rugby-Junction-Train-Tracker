using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMapPinShareCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShareCode",
                table: "MapPins",
                type: "TEXT",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShareCode",
                table: "MapPinHistories",
                type: "TEXT",
                maxLength: 6,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapPins_ShareCode",
                table: "MapPins",
                column: "ShareCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapPinHistories_ShareCode",
                table: "MapPinHistories",
                column: "ShareCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MapPins_ShareCode",
                table: "MapPins");

            migrationBuilder.DropIndex(
                name: "IX_MapPinHistories_ShareCode",
                table: "MapPinHistories");

            migrationBuilder.DropColumn(
                name: "ShareCode",
                table: "MapPins");

            migrationBuilder.DropColumn(
                name: "ShareCode",
                table: "MapPinHistories");
        }
    }
}
