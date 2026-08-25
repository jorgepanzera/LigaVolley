using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScorerOfflineSyncAndTakeOver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "last_accepted_sequence",
                schema: "dbo",
                table: "MATCH_SHEET_SESSION",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "new_device_id",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "new_session_uuid",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "previous_device_id",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "previous_session_uuid",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "local_sequence",
                schema: "dbo",
                table: "MATCH_EVENT",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "match_sheet_session_id",
                schema: "dbo",
                table: "MATCH_EVENT",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sync_payload_hash",
                schema: "dbo",
                table: "MATCH_EVENT",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_SHEET_SESSION_sequence",
                schema: "dbo",
                table: "MATCH_SHEET_SESSION",
                sql: "[last_accepted_sequence] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_SHEET_SESSION_status",
                schema: "dbo",
                table: "MATCH_SHEET_SESSION",
                sql: "[status] IN ('ACTIVE','ABANDONED','CLOSED')");

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_EVENT_session_sequence",
                schema: "dbo",
                table: "MATCH_EVENT",
                columns: new[] { "match_sheet_session_id", "local_sequence" },
                unique: true,
                filter: "[match_sheet_session_id] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_EVENT_local_sequence",
                schema: "dbo",
                table: "MATCH_EVENT",
                sql: "[local_sequence] IS NULL OR [local_sequence] > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_MATCH_EVENT_MATCH_SHEET_SESSION_match_sheet_session_id",
                schema: "dbo",
                table: "MATCH_EVENT",
                column: "match_sheet_session_id",
                principalSchema: "dbo",
                principalTable: "MATCH_SHEET_SESSION",
                principalColumn: "match_sheet_session_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MATCH_EVENT_MATCH_SHEET_SESSION_match_sheet_session_id",
                schema: "dbo",
                table: "MATCH_EVENT");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_SHEET_SESSION_sequence",
                schema: "dbo",
                table: "MATCH_SHEET_SESSION");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_SHEET_SESSION_status",
                schema: "dbo",
                table: "MATCH_SHEET_SESSION");

            migrationBuilder.DropIndex(
                name: "UQ_MATCH_EVENT_session_sequence",
                schema: "dbo",
                table: "MATCH_EVENT");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_EVENT_local_sequence",
                schema: "dbo",
                table: "MATCH_EVENT");

            migrationBuilder.DropColumn(
                name: "last_accepted_sequence",
                schema: "dbo",
                table: "MATCH_SHEET_SESSION");

            migrationBuilder.DropColumn(
                name: "new_device_id",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT");

            migrationBuilder.DropColumn(
                name: "new_session_uuid",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT");

            migrationBuilder.DropColumn(
                name: "previous_device_id",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT");

            migrationBuilder.DropColumn(
                name: "previous_session_uuid",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT");

            migrationBuilder.DropColumn(
                name: "local_sequence",
                schema: "dbo",
                table: "MATCH_EVENT");

            migrationBuilder.DropColumn(
                name: "match_sheet_session_id",
                schema: "dbo",
                table: "MATCH_EVENT");

            migrationBuilder.DropColumn(
                name: "sync_payload_hash",
                schema: "dbo",
                table: "MATCH_EVENT");
        }
    }
}
