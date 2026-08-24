using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClubsTeamsAndVenues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CLUB",
                schema: "dbo",
                columns: table => new
                {
                    club_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    short_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLUB", x => x.club_id);
                });

            migrationBuilder.CreateTable(
                name: "VENUE",
                schema: "dbo",
                columns: table => new
                {
                    venue_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VENUE", x => x.venue_id);
                });

            migrationBuilder.CreateTable(
                name: "TEAM",
                schema: "dbo",
                columns: table => new
                {
                    team_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    club_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    gender = table.Column<string>(type: "char(1)", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TEAM", x => x.team_id);
                    table.CheckConstraint("CK_TEAM_gender", "[gender] IN ('M','F')");
                    table.ForeignKey(
                        name: "FK_TEAM_CLUB",
                        column: x => x.club_id,
                        principalSchema: "dbo",
                        principalTable: "CLUB",
                        principalColumn: "club_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_CLUB_name",
                schema: "dbo",
                table: "CLUB",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TEAM_club_id",
                schema: "dbo",
                table: "TEAM",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "UQ_TEAM_name_gender",
                schema: "dbo",
                table: "TEAM",
                columns: new[] { "name", "gender" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_VENUE_name",
                schema: "dbo",
                table: "VENUE",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TEAM",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "VENUE",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CLUB",
                schema: "dbo");
        }
    }
}
