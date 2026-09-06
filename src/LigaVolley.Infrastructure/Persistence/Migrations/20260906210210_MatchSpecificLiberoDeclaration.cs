using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MatchSpecificLiberoDeclaration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_COMPETITION_ROSTER_PLAYER_role",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER");

            migrationBuilder.RenameIndex(
                name: "IX_COMPETITION_ROSTER_PLAYER_limits",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                newName: "IX_COMPETITION_ROSTER_PLAYER_habitual_function");

            migrationBuilder.AlterColumn<string>(
                name: "player_role",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                type: "varchar(30)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(30)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_COMPETITION_ROSTER_PLAYER_role",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                sql: "[player_role] IS NULL OR [player_role] IN ('SETTER','OUTSIDE_HITTER','MIDDLE_BLOCKER','OPPOSITE','LIBERO')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.COMPETITION_ROSTER_PLAYER WHERE player_role IS NULL)
                   OR EXISTS (
                        SELECT 1
                        FROM dbo.COMPETITION_ROSTER_PLAYER
                        WHERE status = 'ACTIVE' AND player_role = 'LIBERO'
                        GROUP BY competition_roster_id
                        HAVING COUNT(*) > 2)
                THROW 51000, 'Cannot downgrade MatchSpecificLiberoDeclaration: one or more rosters use a nullable habitual function or more than two active habitual LIBERO functions. Archive or convert those rows explicitly before downgrade; no players were changed.', 1;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_COMPETITION_ROSTER_PLAYER_role",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER");

            migrationBuilder.RenameIndex(
                name: "IX_COMPETITION_ROSTER_PLAYER_habitual_function",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                newName: "IX_COMPETITION_ROSTER_PLAYER_limits");

            migrationBuilder.AlterColumn<string>(
                name: "player_role",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                type: "varchar(30)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_COMPETITION_ROSTER_PLAYER_role",
                schema: "dbo",
                table: "COMPETITION_ROSTER_PLAYER",
                sql: "[player_role] IN ('SETTER','OUTSIDE_HITTER','MIDDLE_BLOCKER','OPPOSITE','LIBERO')");
        }
    }
}
