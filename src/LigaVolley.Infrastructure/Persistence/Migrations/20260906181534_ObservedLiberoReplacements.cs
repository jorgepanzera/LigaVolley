using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ObservedLiberoReplacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_SHEET_rules_snapshot",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_SHEET_rules_snapshot",
                schema: "dbo",
                table: "MATCH_SHEET",
                sql: "(([rules_snapshot_version] IN (0,1) AND [rules_protocol_version] = 1) OR ([rules_snapshot_version] = 2 AND [rules_protocol_version] = 2)) AND ([rules_snapshot_version] = 0 OR [max_substitutions_per_set] BETWEEN 1 AND 99 AND [max_substitutions_per_set] IS NOT NULL) AND [max_timeouts_per_set] BETWEEN 1 AND 99");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.MATCH_SHEET WHERE rules_protocol_version >= 2)
                   OR EXISTS (SELECT 1 FROM dbo.MATCH_EVENT WHERE ISJSON(command_payload) = 1
                       AND JSON_VALUE(command_payload, '$.observedLiberoReplacements') = 'true')
                    THROW 51000, 'Observed libero history requires an explicit archival/conversion plan before downgrade.', 1;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_SHEET_rules_snapshot",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_SHEET_rules_snapshot",
                schema: "dbo",
                table: "MATCH_SHEET",
                sql: "[rules_snapshot_version] IN (0,1) AND [rules_protocol_version] = 1 AND ([rules_snapshot_version] = 0 OR [max_substitutions_per_set] BETWEEN 1 AND 99 AND [max_substitutions_per_set] IS NOT NULL) AND [max_timeouts_per_set] BETWEEN 1 AND 99");
        }
    }
}
