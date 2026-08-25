using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchSheetConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_SHEET_sets",
                schema: "dbo",
                table: "MATCH_SHEET",
                sql: "[home_sets] BETWEEN 0 AND 3 AND [away_sets] BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_SHEET_status",
                schema: "dbo",
                table: "MATCH_SHEET",
                sql: "[status] IN ('OPEN','IN_PROGRESS','SUSPENDED','CLOSED','CANCELLED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_SHEET_sets",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_SHEET_status",
                schema: "dbo",
                table: "MATCH_SHEET");
        }
    }
}
