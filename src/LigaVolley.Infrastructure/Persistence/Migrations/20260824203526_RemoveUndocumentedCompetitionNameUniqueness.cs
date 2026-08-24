using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUndocumentedCompetitionNameUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_COMPETITION_season_division_name",
                schema: "dbo",
                table: "COMPETITION");

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_season_id",
                schema: "dbo",
                table: "COMPETITION",
                column: "season_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_COMPETITION_season_id",
                schema: "dbo",
                table: "COMPETITION");

            migrationBuilder.CreateIndex(
                name: "UQ_COMPETITION_season_division_name",
                schema: "dbo",
                table: "COMPETITION",
                columns: new[] { "season_id", "division_id", "name" },
                unique: true);
        }
    }
}
