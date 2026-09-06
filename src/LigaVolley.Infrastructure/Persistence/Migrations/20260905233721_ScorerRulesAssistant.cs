using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScorerRulesAssistant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_TIMEOUT_number",
                schema: "dbo",
                table: "MATCH_TIMEOUT");

            migrationBuilder.AlterColumn<int>(
                name: "timeout_number",
                schema: "dbo",
                table: "MATCH_TIMEOUT",
                type: "int",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AddColumn<Guid>(
                name: "request_event_uuid",
                schema: "dbo",
                table: "MATCH_SUBSTITUTION",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "deciding_set_court_change_point",
                schema: "dbo",
                table: "MATCH_SHEET",
                type: "int",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<bool>(
                name: "libero_can_serve",
                schema: "dbo",
                table: "MATCH_SHEET",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "libero_enabled",
                schema: "dbo",
                table: "MATCH_SHEET",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "max_liberos",
                schema: "dbo",
                table: "MATCH_SHEET",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "max_substitutions_per_set",
                schema: "dbo",
                table: "MATCH_SHEET",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_timeouts_per_set",
                schema: "dbo",
                table: "MATCH_SHEET",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "rules_protocol_version",
                schema: "dbo",
                table: "MATCH_SHEET",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "rules_snapshot_version",
                schema: "dbo",
                table: "MATCH_SHEET",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "command_payload",
                schema: "dbo",
                table: "MATCH_EVENT",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_substitutions_per_set",
                schema: "dbo",
                table: "COMPETITION_FORMAT",
                type: "int",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "max_timeouts_per_set",
                schema: "dbo",
                table: "COMPETITION_FORMAT",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "max_substitutions_per_set_override",
                schema: "dbo",
                table: "COMPETITION",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_timeouts_per_set_override",
                schema: "dbo",
                table: "COMPETITION",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_TIMEOUT_number",
                schema: "dbo",
                table: "MATCH_TIMEOUT",
                sql: "[timeout_number] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SUBSTITUTION_request_event_uuid",
                schema: "dbo",
                table: "MATCH_SUBSTITUTION",
                column: "request_event_uuid");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_SHEET_rules_snapshot",
                schema: "dbo",
                table: "MATCH_SHEET",
                sql: "[rules_snapshot_version] IN (0,1) AND [rules_protocol_version] = 1 AND ([rules_snapshot_version] = 0 OR [max_substitutions_per_set] BETWEEN 1 AND 99 AND [max_substitutions_per_set] IS NOT NULL) AND [max_timeouts_per_set] BETWEEN 1 AND 99");

            migrationBuilder.AddCheckConstraint(
                name: "CK_COMPETITION_FORMAT_match_rules",
                schema: "dbo",
                table: "COMPETITION_FORMAT",
                sql: "[max_substitutions_per_set] BETWEEN 1 AND 99 AND [max_timeouts_per_set] BETWEEN 1 AND 99");

            migrationBuilder.AddCheckConstraint(
                name: "CK_COMPETITION_match_rules",
                schema: "dbo",
                table: "COMPETITION",
                sql: "([max_substitutions_per_set_override] IS NULL OR [max_substitutions_per_set_override] BETWEEN 1 AND 99) AND ([max_timeouts_per_set_override] IS NULL OR [max_timeouts_per_set_override] BETWEEN 1 AND 99)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older binaries cannot interpret confirmed decisions or frozen rules. Never silently discard them.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [dbo].[MATCH_SHEET] WHERE [rules_snapshot_version] > 0) OR EXISTS (SELECT 1 FROM [dbo].[MATCH_EVENT] WHERE [command_payload] IS NOT NULL) OR EXISTS (SELECT 1 FROM [dbo].[MATCH_TIMEOUT] WHERE [timeout_number] > 2) THROW 51000, 'ScorerRulesAssistant rollback requires an explicit archival/migration plan for operational data.', 1;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_TIMEOUT_number",
                schema: "dbo",
                table: "MATCH_TIMEOUT");

            migrationBuilder.DropIndex(
                name: "IX_MATCH_SUBSTITUTION_request_event_uuid",
                schema: "dbo",
                table: "MATCH_SUBSTITUTION");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_SHEET_rules_snapshot",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropCheckConstraint(
                name: "CK_COMPETITION_FORMAT_match_rules",
                schema: "dbo",
                table: "COMPETITION_FORMAT");

            migrationBuilder.DropCheckConstraint(
                name: "CK_COMPETITION_match_rules",
                schema: "dbo",
                table: "COMPETITION");

            migrationBuilder.DropColumn(
                name: "request_event_uuid",
                schema: "dbo",
                table: "MATCH_SUBSTITUTION");

            migrationBuilder.DropColumn(
                name: "deciding_set_court_change_point",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropColumn(
                name: "libero_can_serve",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropColumn(
                name: "libero_enabled",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropColumn(
                name: "max_liberos",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropColumn(
                name: "max_substitutions_per_set",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropColumn(
                name: "max_timeouts_per_set",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropColumn(
                name: "rules_protocol_version",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropColumn(
                name: "rules_snapshot_version",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropColumn(
                name: "command_payload",
                schema: "dbo",
                table: "MATCH_EVENT");

            migrationBuilder.DropColumn(
                name: "max_substitutions_per_set",
                schema: "dbo",
                table: "COMPETITION_FORMAT");

            migrationBuilder.DropColumn(
                name: "max_timeouts_per_set",
                schema: "dbo",
                table: "COMPETITION_FORMAT");

            migrationBuilder.DropColumn(
                name: "max_substitutions_per_set_override",
                schema: "dbo",
                table: "COMPETITION");

            migrationBuilder.DropColumn(
                name: "max_timeouts_per_set_override",
                schema: "dbo",
                table: "COMPETITION");

            migrationBuilder.AlterColumn<byte>(
                name: "timeout_number",
                schema: "dbo",
                table: "MATCH_TIMEOUT",
                type: "tinyint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_TIMEOUT_number",
                schema: "dbo",
                table: "MATCH_TIMEOUT",
                sql: "[timeout_number] BETWEEN 1 AND 2");
        }
    }
}
