using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MatchSpecificJerseyNumberAndCaptain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_COMPETITION_ROSTER_PLAYER_active_jersey",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER");

            migrationBuilder.DropColumn(
                name: "jersey_number",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER");

            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [dbo].[MATCH_PLAYER] WHERE [jersey_number] IS NULL) THROW 51000, 'Cannot make MATCH_PLAYER.jersey_number required because historical rows are incomplete.', 1;");

            migrationBuilder.AlterColumn<short>(
                name: "jersey_number",
                schema: "dbo",
                table: "MATCH_PLAYER",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_PLAYER_jersey",
                schema: "dbo",
                table: "MATCH_PLAYER",
                sql: "[jersey_number] BETWEEN 1 AND 99");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_PLAYER_jersey",
                schema: "dbo",
                table: "MATCH_PLAYER");

            migrationBuilder.AlterColumn<short>(
                name: "jersey_number",
                schema: "dbo",
                table: "MATCH_PLAYER",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AddColumn<short>(
                name: "jersey_number",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_COMPETITION_ROSTER_PLAYER_active_jersey",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                columns: new[] { "competition_roster_id", "jersey_number" },
                unique: true,
                filter: "[status] = 'ACTIVE' AND [jersey_number] IS NOT NULL");
        }
    }
}
