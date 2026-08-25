using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionRosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COMPETITION_ROSTER",
                schema: "dbo",
                columns: table => new
                {
                    competition_roster_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    team_entry_id = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPETITION_ROSTER", x => x.competition_roster_id);
                    table.CheckConstraint("CK_COMPETITION_ROSTER_status", "[status] IN ('DRAFT','ACTIVE','CLOSED')");
                    table.ForeignKey(
                        name: "FK_COMPETITION_ROSTER_TEAM_ENTRY",
                        column: x => x.team_entry_id,
                        principalSchema: "dbo",
                        principalTable: "TEAM_ENTRY",
                        principalColumn: "team_entry_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COMPETITION_ROSTER_PLAYER",
                schema: "dbo",
                columns: table => new
                {
                    competition_roster_player_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_roster_id = table.Column<int>(type: "int", nullable: false),
                    player_id = table.Column<int>(type: "int", nullable: false),
                    jersey_number = table.Column<short>(type: "smallint", nullable: true),
                    player_role = table.Column<string>(type: "varchar(30)", nullable: false),
                    status = table.Column<string>(type: "varchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPETITION_ROSTER_PLAYER", x => x.competition_roster_player_id);
                    table.CheckConstraint("CK_COMPETITION_ROSTER_PLAYER_role", "[player_role] IN ('SETTER','OUTSIDE_HITTER','MIDDLE_BLOCKER','OPPOSITE','LIBERO')");
                    table.CheckConstraint("CK_COMPETITION_ROSTER_PLAYER_status", "[status] IN ('ACTIVE','INACTIVE')");
                    table.ForeignKey(
                        name: "FK_COMPETITION_ROSTER_PLAYER_PLAYER",
                        column: x => x.player_id,
                        principalSchema: "dbo",
                        principalTable: "PLAYER",
                        principalColumn: "player_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPETITION_ROSTER_PLAYER_ROSTER",
                        column: x => x.competition_roster_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_ROSTER",
                        principalColumn: "competition_roster_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COMPETITION_ROSTER_STAFF",
                schema: "dbo",
                columns: table => new
                {
                    competition_roster_staff_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_roster_id = table.Column<int>(type: "int", nullable: false),
                    coach_id = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPETITION_ROSTER_STAFF", x => x.competition_roster_staff_id);
                    table.CheckConstraint("CK_COMPETITION_ROSTER_STAFF_status", "[status] IN ('ACTIVE','INACTIVE')");
                    table.ForeignKey(
                        name: "FK_COMPETITION_ROSTER_STAFF_COACH",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "COACH",
                        principalColumn: "coach_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPETITION_ROSTER_STAFF_ROSTER",
                        column: x => x.competition_roster_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_ROSTER",
                        principalColumn: "competition_roster_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_COMPETITION_ROSTER_team_entry",
                schema: "dbo",
                table: "COMPETITION_ROSTER",
                column: "team_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_ROSTER_PLAYER_limits",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                columns: new[] { "competition_roster_id", "status", "player_role" });

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_ROSTER_PLAYER_player_id",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "UQ_COMPETITION_ROSTER_PLAYER_player",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                columns: new[] { "competition_roster_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_COMPETITION_ROSTER_PLAYER_active_jersey",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                columns: new[] { "competition_roster_id", "jersey_number" },
                unique: true,
                filter: "[status] = 'ACTIVE' AND [jersey_number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_ROSTER_STAFF_coach_id",
                schema: "dbo",
                table: "COMPETITION_ROSTER_STAFF",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_ROSTER_STAFF_limit",
                schema: "dbo",
                table: "COMPETITION_ROSTER_STAFF",
                columns: new[] { "competition_roster_id", "status" });

            migrationBuilder.CreateIndex(
                name: "UQ_COMPETITION_ROSTER_STAFF_coach",
                schema: "dbo",
                table: "COMPETITION_ROSTER_STAFF",
                columns: new[] { "competition_roster_id", "coach_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COMPETITION_ROSTER_PLAYER",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COMPETITION_ROSTER_STAFF",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COMPETITION_ROSTER",
                schema: "dbo");
        }
    }
}
