using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayoffSeriesProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "winner_team_entry_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PLAYOFF_SERIES_winner_team_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                columns: new[] { "winner_team_entry_id", "competition_id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PLAYOFF_SERIES_winner",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                sql: "[winner_team_entry_id] IS NULL OR [winner_team_entry_id] IN ([team1_entry_id], [team2_entry_id])");

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_series_number",
                schema: "dbo",
                table: "MATCH",
                columns: new[] { "series_id", "match_number" },
                unique: true,
                filter: "[series_id] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PLAYOFF_SERIES_TEAM_ENTRY_winner_team_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                columns: new[] { "winner_team_entry_id", "competition_id" },
                principalSchema: "dbo",
                principalTable: "TEAM_ENTRY",
                principalColumns: new[] { "team_entry_id", "competition_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PLAYOFF_SERIES_TEAM_ENTRY_winner_team_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropIndex(
                name: "IX_PLAYOFF_SERIES_winner_team_entry_id_competition_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PLAYOFF_SERIES_winner",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.DropIndex(
                name: "UQ_MATCH_series_number",
                schema: "dbo",
                table: "MATCH");

            migrationBuilder.DropColumn(
                name: "winner_team_entry_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");
        }
    }
}
