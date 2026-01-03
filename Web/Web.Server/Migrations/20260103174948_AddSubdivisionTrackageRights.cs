using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSubdivisionTrackageRights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubdivisionTrackageRights",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromSubdivisionID = table.Column<int>(type: "INTEGER", nullable: false),
                    ToSubdivisionID = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubdivisionTrackageRights", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SubdivisionTrackageRights_Subdivisions_FromSubdivisionID",
                        column: x => x.FromSubdivisionID,
                        principalTable: "Subdivisions",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubdivisionTrackageRights_Subdivisions_ToSubdivisionID",
                        column: x => x.ToSubdivisionID,
                        principalTable: "Subdivisions",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubdivisionTrackageRights_FromSubdivisionID",
                table: "SubdivisionTrackageRights",
                column: "FromSubdivisionID");

            migrationBuilder.CreateIndex(
                name: "IX_SubdivisionTrackageRights_ToSubdivisionID",
                table: "SubdivisionTrackageRights",
                column: "ToSubdivisionID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubdivisionTrackageRights");
        }
    }
}
