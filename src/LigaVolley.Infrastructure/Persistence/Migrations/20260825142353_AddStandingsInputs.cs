using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStandingsInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MATCH_SET",
                schema: "dbo",
                columns: table => new
                {
                    match_set_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_id = table.Column<int>(type: "int", nullable: false),
                    set_number = table.Column<byte>(type: "tinyint", nullable: false),
                    home_points = table.Column<short>(type: "smallint", nullable: false),
                    away_points = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_SET", x => x.match_set_id);
                    table.CheckConstraint("CK_MATCH_SET_number", "[set_number] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_MATCH_SET_points", "[home_points] >= 0 AND [away_points] >= 0");
                    table.ForeignKey(
                        name: "FK_MATCH_SET_MATCH",
                        column: x => x.match_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH",
                        principalColumn: "match_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PHASE_GROUP_ENTRY",
                schema: "dbo",
                columns: table => new
                {
                    phase_group_entry_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_id = table.Column<int>(type: "int", nullable: false),
                    phase_group_id = table.Column<int>(type: "int", nullable: false),
                    team_entry_id = table.Column<int>(type: "int", nullable: false),
                    source_position = table.Column<short>(type: "smallint", nullable: true),
                    seed = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PHASE_GROUP_ENTRY", x => x.phase_group_entry_id);
                    table.CheckConstraint("CK_PHASE_GROUP_ENTRY_seed", "[seed] IS NULL OR [seed] > 0");
                    table.CheckConstraint("CK_PHASE_GROUP_ENTRY_source_position", "[source_position] IS NULL OR [source_position] > 0");
                    table.ForeignKey(
                        name: "FK_PHASE_GROUP_ENTRY_PHASE_GROUP_phase_group_id",
                        column: x => x.phase_group_id,
                        principalSchema: "dbo",
                        principalTable: "PHASE_GROUP",
                        principalColumn: "phase_group_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PHASE_GROUP_ENTRY_TEAM",
                        columns: x => new { x.team_entry_id, x.competition_id },
                        principalSchema: "dbo",
                        principalTable: "TEAM_ENTRY",
                        principalColumns: new[] { "team_entry_id", "competition_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_SET",
                schema: "dbo",
                table: "MATCH_SET",
                columns: new[] { "match_id", "set_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PHASE_GROUP_ENTRY_team_entry_id_competition_id",
                schema: "dbo",
                table: "PHASE_GROUP_ENTRY",
                columns: new[] { "team_entry_id", "competition_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_PHASE_GROUP_ENTRY",
                schema: "dbo",
                table: "PHASE_GROUP_ENTRY",
                columns: new[] { "phase_group_id", "team_entry_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MATCH_SET",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PHASE_GROUP_ENTRY",
                schema: "dbo");
        }
    }
}
