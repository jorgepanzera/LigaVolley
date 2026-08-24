using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialFixture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "UQ_PLAYOFF_SERIES_id_phase",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                columns: new[] { "playoff_series_id", "competition_phase_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_PHASE_GROUP_id_phase",
                schema: "dbo",
                table: "PHASE_GROUP",
                columns: new[] { "phase_group_id", "competition_phase_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_COMPETITION_PHASE_id_comp",
                schema: "dbo",
                table: "COMPETITION_PHASE",
                columns: new[] { "competition_phase_id", "competition_id" });

            migrationBuilder.CreateTable(
                name: "FIXTURE_GENERATION",
                schema: "dbo",
                columns: table => new
                {
                    fixture_generation_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_id = table.Column<int>(type: "int", nullable: false),
                    phase_id = table.Column<int>(type: "int", nullable: false),
                    phase_group_id = table.Column<int>(type: "int", nullable: true),
                    random_seed = table.Column<int>(type: "int", nullable: false),
                    generated_at = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FIXTURE_GENERATION", x => x.fixture_generation_id);
                    table.ForeignKey(
                        name: "FK_FIXTURE_GENERATION_COMPETITION",
                        column: x => x.competition_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION",
                        principalColumn: "competition_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FIXTURE_GENERATION_GROUP",
                        columns: x => new { x.phase_group_id, x.phase_id },
                        principalSchema: "dbo",
                        principalTable: "PHASE_GROUP",
                        principalColumns: new[] { "phase_group_id", "competition_phase_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FIXTURE_GENERATION_PHASE",
                        columns: x => new { x.phase_id, x.competition_id },
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_PHASE",
                        principalColumns: new[] { "competition_phase_id", "competition_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH",
                schema: "dbo",
                columns: table => new
                {
                    match_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_id = table.Column<int>(type: "int", nullable: false),
                    phase_id = table.Column<int>(type: "int", nullable: false),
                    phase_group_id = table.Column<int>(type: "int", nullable: true),
                    series_id = table.Column<int>(type: "int", nullable: true),
                    home_team_entry_id = table.Column<int>(type: "int", nullable: true),
                    away_team_entry_id = table.Column<int>(type: "int", nullable: true),
                    match_date = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    venue_id = table.Column<int>(type: "int", nullable: true),
                    round_number = table.Column<short>(type: "smallint", nullable: false),
                    match_number = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "PENDING"),
                    home_sets = table.Column<byte>(type: "tinyint", nullable: true),
                    away_sets = table.Column<byte>(type: "tinyint", nullable: true),
                    winner_team_entry_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH", x => x.match_id);
                    table.UniqueConstraint("UQ_MATCH_id_comp", x => new { x.match_id, x.competition_id });
                    table.CheckConstraint("CK_MATCH_different_teams", "[home_team_entry_id] IS NULL OR [away_team_entry_id] IS NULL OR [home_team_entry_id] <> [away_team_entry_id]");
                    table.CheckConstraint("CK_MATCH_group_or_series", "NOT ([phase_group_id] IS NOT NULL AND [series_id] IS NOT NULL)");
                    table.CheckConstraint("CK_MATCH_match_number", "[match_number] > 0");
                    table.CheckConstraint("CK_MATCH_round_number", "[round_number] > 0");
                    table.CheckConstraint("CK_MATCH_sets", "([home_sets] IS NULL AND [away_sets] IS NULL) OR ([home_sets] BETWEEN 0 AND 3 AND [away_sets] BETWEEN 0 AND 3 AND NOT ([home_sets] = 3 AND [away_sets] = 3))");
                    table.CheckConstraint("CK_MATCH_status", "[status] IN ('PENDING','SCHEDULED','IN_PROGRESS','FINISHED','SUSPENDED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_MATCH_AWAY_TEAM",
                        columns: x => new { x.away_team_entry_id, x.competition_id },
                        principalSchema: "dbo",
                        principalTable: "TEAM_ENTRY",
                        principalColumns: new[] { "team_entry_id", "competition_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_COMPETITION_competition_id",
                        column: x => x.competition_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION",
                        principalColumn: "competition_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_HOME_TEAM",
                        columns: x => new { x.home_team_entry_id, x.competition_id },
                        principalSchema: "dbo",
                        principalTable: "TEAM_ENTRY",
                        principalColumns: new[] { "team_entry_id", "competition_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_PHASE",
                        columns: x => new { x.phase_id, x.competition_id },
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_PHASE",
                        principalColumns: new[] { "competition_phase_id", "competition_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_PHASE_GROUP",
                        columns: x => new { x.phase_group_id, x.phase_id },
                        principalSchema: "dbo",
                        principalTable: "PHASE_GROUP",
                        principalColumns: new[] { "phase_group_id", "competition_phase_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_SERIES",
                        columns: x => new { x.series_id, x.phase_id },
                        principalSchema: "dbo",
                        principalTable: "PLAYOFF_SERIES",
                        principalColumns: new[] { "playoff_series_id", "competition_phase_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_VENUE",
                        column: x => x.venue_id,
                        principalSchema: "dbo",
                        principalTable: "VENUE",
                        principalColumn: "venue_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_WINNER",
                        columns: x => new { x.winner_team_entry_id, x.competition_id },
                        principalSchema: "dbo",
                        principalTable: "TEAM_ENTRY",
                        principalColumns: new[] { "team_entry_id", "competition_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FIXTURE_GENERATION_phase_group_id_phase_id",
                schema: "dbo",
                table: "FIXTURE_GENERATION",
                columns: new[] { "phase_group_id", "phase_id" });

            migrationBuilder.CreateIndex(
                name: "IX_FIXTURE_GENERATION_phase_id_competition_id",
                schema: "dbo",
                table: "FIXTURE_GENERATION",
                columns: new[] { "phase_id", "competition_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_FIXTURE_GENERATION_group_scope",
                schema: "dbo",
                table: "FIXTURE_GENERATION",
                columns: new[] { "competition_id", "phase_id", "phase_group_id" },
                unique: true,
                filter: "[phase_group_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_FIXTURE_GENERATION_phase_scope",
                schema: "dbo",
                table: "FIXTURE_GENERATION",
                columns: new[] { "competition_id", "phase_id" },
                unique: true,
                filter: "[phase_group_id] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_away_team_entry_id_competition_id",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "away_team_entry_id", "competition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_competition_date",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "competition_id", "match_date" });

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_home_team_entry_id_competition_id",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "home_team_entry_id", "competition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_phase_group_id_phase_id",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "phase_group_id", "phase_id" });

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_phase_id_competition_id",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "phase_id", "competition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_phase_round",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "phase_id", "round_number" });

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_series_id_phase_id",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "series_id", "phase_id" });

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_venue_id",
                schema: "dbo",
                table: "MATCH",
                column: "venue_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_winner_team_entry_id_competition_id",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "winner_team_entry_id", "competition_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_group_scope_number",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "competition_id", "phase_id", "phase_group_id", "match_number" },
                unique: true,
                filter: "[phase_group_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_phase_scope_number",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "competition_id", "phase_id", "match_number" },
                unique: true,
                filter: "[phase_group_id] IS NULL AND [series_id] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FIXTURE_GENERATION",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH",
                schema: "dbo");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_PLAYOFF_SERIES_id_phase",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_PHASE_GROUP_id_phase",
                schema: "dbo",
                table: "PHASE_GROUP");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_COMPETITION_PHASE_id_comp",
                schema: "dbo",
                table: "COMPETITION_PHASE");
        }
    }
}
