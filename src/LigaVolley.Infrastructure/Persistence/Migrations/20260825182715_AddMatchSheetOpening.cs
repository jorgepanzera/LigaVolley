using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchSheetOpening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MATCH_SHEET",
                schema: "dbo",
                columns: table => new
                {
                    match_sheet_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    sheet_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_id = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    home_sets = table.Column<byte>(type: "tinyint", nullable: false),
                    away_sets = table.Column<byte>(type: "tinyint", nullable: false),
                    winner_team_entry_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_SHEET", x => x.match_sheet_id);
                    table.ForeignKey(
                        name: "FK_MATCH_SHEET_MATCH_match_id",
                        column: x => x.match_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH",
                        principalColumn: "match_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_SHEET_SESSION",
                schema: "dbo",
                columns: table => new
                {
                    match_sheet_session_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    session_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_sheet_id = table.Column<int>(type: "int", nullable: false),
                    match_official_id = table.Column<int>(type: "int", nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_SHEET_SESSION", x => x.match_sheet_session_id);
                    table.ForeignKey(
                        name: "FK_MATCH_SHEET_SESSION_MATCH_OFFICIAL_match_official_id",
                        column: x => x.match_official_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_OFFICIAL",
                        principalColumn: "match_official_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_SHEET_SESSION_MATCH_SHEET_match_sheet_id",
                        column: x => x.match_sheet_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SHEET",
                        principalColumn: "match_sheet_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_TEAM",
                schema: "dbo",
                columns: table => new
                {
                    match_team_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_team_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_sheet_id = table.Column<int>(type: "int", nullable: false),
                    team_entry_id = table.Column<int>(type: "int", nullable: false),
                    competition_roster_id = table.Column<int>(type: "int", nullable: false),
                    side = table.Column<string>(type: "varchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_TEAM", x => x.match_team_id);
                    table.ForeignKey(
                        name: "FK_MATCH_TEAM_COMPETITION_ROSTER_competition_roster_id",
                        column: x => x.competition_roster_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_ROSTER",
                        principalColumn: "competition_roster_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_TEAM_MATCH_SHEET_match_sheet_id",
                        column: x => x.match_sheet_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SHEET",
                        principalColumn: "match_sheet_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_TEAM_TEAM_ENTRY_team_entry_id",
                        column: x => x.team_entry_id,
                        principalSchema: "dbo",
                        principalTable: "TEAM_ENTRY",
                        principalColumn: "team_entry_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_SHEET_AUDIT",
                schema: "dbo",
                columns: table => new
                {
                    match_sheet_audit_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    audit_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_sheet_id = table.Column<int>(type: "int", nullable: false),
                    match_sheet_session_id = table.Column<int>(type: "int", nullable: false),
                    event_type = table.Column<string>(type: "varchar(30)", nullable: false),
                    client_request_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_SHEET_AUDIT", x => x.match_sheet_audit_id);
                    table.ForeignKey(
                        name: "FK_MATCH_SHEET_AUDIT_MATCH_SHEET_SESSION_match_sheet_session_id",
                        column: x => x.match_sheet_session_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SHEET_SESSION",
                        principalColumn: "match_sheet_session_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_SHEET_AUDIT_MATCH_SHEET_match_sheet_id",
                        column: x => x.match_sheet_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SHEET",
                        principalColumn: "match_sheet_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_PLAYER",
                schema: "dbo",
                columns: table => new
                {
                    match_player_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_player_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_team_id = table.Column<int>(type: "int", nullable: false),
                    competition_roster_player_id = table.Column<int>(type: "int", nullable: false),
                    jersey_number = table.Column<short>(type: "smallint", nullable: true),
                    is_match_captain = table.Column<bool>(type: "bit", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_PLAYER", x => x.match_player_id);
                    table.ForeignKey(
                        name: "FK_MATCH_PLAYER_COMPETITION_ROSTER_PLAYER_competition_roster_player_id",
                        column: x => x.competition_roster_player_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_ROSTER_PLAYER",
                        principalColumn: "competition_roster_player_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_PLAYER_MATCH_TEAM_match_team_id",
                        column: x => x.match_team_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_TEAM",
                        principalColumn: "match_team_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_TEAM_STAFF",
                schema: "dbo",
                columns: table => new
                {
                    match_team_staff_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_team_staff_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_team_id = table.Column<int>(type: "int", nullable: false),
                    competition_roster_staff_id = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_TEAM_STAFF", x => x.match_team_staff_id);
                    table.ForeignKey(
                        name: "FK_MATCH_TEAM_STAFF_COMPETITION_ROSTER_STAFF_competition_roster_staff_id",
                        column: x => x.competition_roster_staff_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_ROSTER_STAFF",
                        principalColumn: "competition_roster_staff_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_TEAM_STAFF_MATCH_TEAM_match_team_id",
                        column: x => x.match_team_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_TEAM",
                        principalColumn: "match_team_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_LIBERO",
                schema: "dbo",
                columns: table => new
                {
                    match_libero_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_libero_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_team_id = table.Column<int>(type: "int", nullable: false),
                    match_player_id = table.Column<int>(type: "int", nullable: false),
                    libero_order = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_LIBERO", x => x.match_libero_id);
                    table.ForeignKey(
                        name: "FK_MATCH_LIBERO_MATCH_PLAYER_match_player_id",
                        column: x => x.match_player_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_PLAYER",
                        principalColumn: "match_player_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_LIBERO_MATCH_TEAM_match_team_id",
                        column: x => x.match_team_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_TEAM",
                        principalColumn: "match_team_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LIBERO_match_libero_uuid",
                schema: "dbo",
                table: "MATCH_LIBERO",
                column: "match_libero_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LIBERO_match_player_id",
                schema: "dbo",
                table: "MATCH_LIBERO",
                column: "match_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LIBERO_match_team_id_libero_order",
                schema: "dbo",
                table: "MATCH_LIBERO",
                columns: new[] { "match_team_id", "libero_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LIBERO_match_team_id_match_player_id",
                schema: "dbo",
                table: "MATCH_LIBERO",
                columns: new[] { "match_team_id", "match_player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_PLAYER_competition_roster_player_id",
                schema: "dbo",
                table: "MATCH_PLAYER",
                column: "competition_roster_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_PLAYER_match_player_uuid",
                schema: "dbo",
                table: "MATCH_PLAYER",
                column: "match_player_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_PLAYER_match_team_id_competition_roster_player_id",
                schema: "dbo",
                table: "MATCH_PLAYER",
                columns: new[] { "match_team_id", "competition_roster_player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_PLAYER_match_team_id_jersey_number",
                schema: "dbo",
                table: "MATCH_PLAYER",
                columns: new[] { "match_team_id", "jersey_number" },
                unique: true,
                filter: "[jersey_number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_MATCH_PLAYER_captain",
                schema: "dbo",
                table: "MATCH_PLAYER",
                column: "match_team_id",
                unique: true,
                filter: "[is_match_captain] = 1");

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_SHEET_match",
                schema: "dbo",
                table: "MATCH_SHEET",
                column: "match_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_SHEET_uuid",
                schema: "dbo",
                table: "MATCH_SHEET",
                column: "sheet_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SHEET_AUDIT_audit_uuid",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT",
                column: "audit_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SHEET_AUDIT_match_sheet_id_client_request_id",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT",
                columns: new[] { "match_sheet_id", "client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SHEET_AUDIT_match_sheet_session_id",
                schema: "dbo",
                table: "MATCH_SHEET_AUDIT",
                column: "match_sheet_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SHEET_SESSION_match_official_id",
                schema: "dbo",
                table: "MATCH_SHEET_SESSION",
                column: "match_official_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SHEET_SESSION_session_uuid",
                schema: "dbo",
                table: "MATCH_SHEET_SESSION",
                column: "session_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MATCH_SHEET_SESSION_active",
                schema: "dbo",
                table: "MATCH_SHEET_SESSION",
                column: "match_sheet_id",
                unique: true,
                filter: "[status] = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_TEAM_competition_roster_id",
                schema: "dbo",
                table: "MATCH_TEAM",
                column: "competition_roster_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_TEAM_match_sheet_id_side",
                schema: "dbo",
                table: "MATCH_TEAM",
                columns: new[] { "match_sheet_id", "side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_TEAM_match_team_uuid",
                schema: "dbo",
                table: "MATCH_TEAM",
                column: "match_team_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_TEAM_team_entry_id",
                schema: "dbo",
                table: "MATCH_TEAM",
                column: "team_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_TEAM_STAFF_competition_roster_staff_id",
                schema: "dbo",
                table: "MATCH_TEAM_STAFF",
                column: "competition_roster_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_TEAM_STAFF_match_team_id_competition_roster_staff_id",
                schema: "dbo",
                table: "MATCH_TEAM_STAFF",
                columns: new[] { "match_team_id", "competition_roster_staff_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_TEAM_STAFF_match_team_staff_uuid",
                schema: "dbo",
                table: "MATCH_TEAM_STAFF",
                column: "match_team_staff_uuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MATCH_LIBERO",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_SHEET_AUDIT",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_TEAM_STAFF",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_PLAYER",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_SHEET_SESSION",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_TEAM",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_SHEET",
                schema: "dbo");
        }
    }
}
