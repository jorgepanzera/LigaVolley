using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhaseCompletionSeriesParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PLAYOFF_SERIES_COMPETITION_PHASE_competition_phase_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.AddColumn<int>(
                name: "competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE ps SET competition_id = cp.competition_id FROM dbo.PLAYOFF_SERIES ps INNER JOIN dbo.COMPETITION_PHASE cp ON cp.competition_phase_id = ps.competition_phase_id;");

            migrationBuilder.AddColumn<int>(
                name: "team1_entry_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "team2_entry_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PLAYOFF_SERIES_competition_phase_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                columns: new[] { "competition_phase_id", "competition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_PLAYOFF_SERIES_team1_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                columns: new[] { "team1_entry_id", "competition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_PLAYOFF_SERIES_team2_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                columns: new[] { "team2_entry_id", "competition_id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PLAYOFF_SERIES_different_teams",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                sql: "[team1_entry_id] IS NULL OR [team2_entry_id] IS NULL OR [team1_entry_id] <> [team2_entry_id]");

            migrationBuilder.AddForeignKey(
                name: "FK_PLAYOFF_SERIES_COMPETITION_PHASE_competition_phase_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                columns: new[] { "competition_phase_id", "competition_id" },
                principalSchema: "dbo",
                principalTable: "COMPETITION_PHASE",
                principalColumns: new[] { "competition_phase_id", "competition_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PLAYOFF_SERIES_TEAM_ENTRY_team1_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                columns: new[] { "team1_entry_id", "competition_id" },
                principalSchema: "dbo",
                principalTable: "TEAM_ENTRY",
                principalColumns: new[] { "team_entry_id", "competition_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PLAYOFF_SERIES_TEAM_ENTRY_team2_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                columns: new[] { "team2_entry_id", "competition_id" },
                principalSchema: "dbo",
                principalTable: "TEAM_ENTRY",
                principalColumns: new[] { "team_entry_id", "competition_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PLAYOFF_SERIES_COMPETITION_PHASE_competition_phase_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropForeignKey(
                name: "FK_PLAYOFF_SERIES_TEAM_ENTRY_team1_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropForeignKey(
                name: "FK_PLAYOFF_SERIES_TEAM_ENTRY_team2_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropIndex(
                name: "IX_PLAYOFF_SERIES_competition_phase_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropIndex(
                name: "IX_PLAYOFF_SERIES_team1_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropIndex(
                name: "IX_PLAYOFF_SERIES_team2_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PLAYOFF_SERIES_different_teams",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropColumn(
                name: "competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropColumn(
                name: "team1_entry_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropColumn(
                name: "team2_entry_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.AddForeignKey(
                name: "FK_PLAYOFF_SERIES_COMPETITION_PHASE_competition_phase_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                column: "competition_phase_id",
                principalSchema: "dbo",
                principalTable: "COMPETITION_PHASE",
                principalColumn: "competition_phase_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
