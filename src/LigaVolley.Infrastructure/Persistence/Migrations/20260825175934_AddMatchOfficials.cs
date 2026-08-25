using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchOfficials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MATCH_OFFICIAL",
                schema: "dbo",
                columns: table => new
                {
                    match_official_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_id = table.Column<int>(type: "int", nullable: false),
                    referee_id = table.Column<int>(type: "int", nullable: false),
                    role = table.Column<string>(type: "varchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_OFFICIAL", x => x.match_official_id);
                    table.CheckConstraint("CK_MATCH_OFFICIAL_role", "[role] IN ('FIRST_REFEREE','SECOND_REFEREE','SCORER')");
                    table.ForeignKey(
                        name: "FK_MATCH_OFFICIAL_MATCH_match_id",
                        column: x => x.match_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH",
                        principalColumn: "match_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_OFFICIAL_REFEREE_referee_id",
                        column: x => x.referee_id,
                        principalSchema: "dbo",
                        principalTable: "REFEREE",
                        principalColumn: "referee_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_OFFICIAL_referee_id",
                schema: "dbo",
                table: "MATCH_OFFICIAL",
                column: "referee_id");

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_OFFICIAL_referee",
                schema: "dbo",
                table: "MATCH_OFFICIAL",
                columns: new[] { "match_id", "referee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_OFFICIAL_role",
                schema: "dbo",
                table: "MATCH_OFFICIAL",
                columns: new[] { "match_id", "role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MATCH_OFFICIAL",
                schema: "dbo");
        }
    }
}
