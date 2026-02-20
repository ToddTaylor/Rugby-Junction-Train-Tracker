using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCustodianToSubdivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustodianId",
                table: "Subdivisions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_CustodianId",
                table: "Subdivisions",
                column: "CustodianId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subdivisions_Users_CustodianId",
                table: "Subdivisions",
                column: "CustodianId",
                principalTable: "Users",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subdivisions_Users_CustodianId",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_CustodianId",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "CustodianId",
                table: "Subdivisions");
        }
    }
}
