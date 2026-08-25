using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddElectronicScoresheetMatchEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "track_libero_replacements",
                schema: "dbo",
                table: "MATCH_SHEET",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "track_substitutions",
                schema: "dbo",
                table: "MATCH_SHEET",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<byte>(
                name: "away_rotation_offset",
                schema: "dbo",
                table: "MATCH_SET",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "current_serving_side",
                schema: "dbo",
                table: "MATCH_SET",
                type: "varchar(10)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "finished_at",
                schema: "dbo",
                table: "MATCH_SET",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "home_rotation_offset",
                schema: "dbo",
                table: "MATCH_SET",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "initial_serving_side",
                schema: "dbo",
                table: "MATCH_SET",
                type: "varchar(10)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "match_sheet_id",
                schema: "dbo",
                table: "MATCH_SET",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "set_uuid",
                schema: "dbo",
                table: "MATCH_SET",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "started_at",
                schema: "dbo",
                table: "MATCH_SET",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "dbo",
                table: "MATCH_SET",
                type: "varchar(20)",
                nullable: false,
                defaultValue: "FINISHED");

            migrationBuilder.AddColumn<string>(
                name: "winner_side",
                schema: "dbo",
                table: "MATCH_SET",
                type: "varchar(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MATCH_EVENT",
                schema: "dbo",
                columns: table => new
                {
                    match_event_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_sheet_id = table.Column<int>(type: "int", nullable: false),
                    match_set_id = table.Column<int>(type: "int", nullable: true),
                    event_type = table.Column<string>(type: "varchar(30)", nullable: false),
                    sequence_number = table.Column<long>(type: "bigint", nullable: false),
                    side = table.Column<string>(type: "varchar(10)", nullable: true),
                    match_player_id = table.Column<int>(type: "int", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false),
                    related_event_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_EVENT", x => x.match_event_id);
                    table.CheckConstraint("CK_MATCH_EVENT_sequence", "[sequence_number] > 0");
                    table.CheckConstraint("CK_MATCH_EVENT_status", "[status] IN ('ACTIVE','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_MATCH_EVENT_MATCH_EVENT_related_event_id",
                        column: x => x.related_event_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_EVENT",
                        principalColumn: "match_event_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_EVENT_MATCH_PLAYER_match_player_id",
                        column: x => x.match_player_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_PLAYER",
                        principalColumn: "match_player_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_EVENT_MATCH_SET_match_set_id",
                        column: x => x.match_set_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SET",
                        principalColumn: "match_set_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_EVENT_MATCH_SHEET_match_sheet_id",
                        column: x => x.match_sheet_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SHEET",
                        principalColumn: "match_sheet_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_LIBERO_REPLACEMENT",
                schema: "dbo",
                columns: table => new
                {
                    match_libero_replacement_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    replacement_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_set_id = table.Column<int>(type: "int", nullable: false),
                    match_team_id = table.Column<int>(type: "int", nullable: false),
                    libero_match_player_id = table.Column<int>(type: "int", nullable: false),
                    replaced_match_player_id = table.Column<int>(type: "int", nullable: false),
                    lineup_position = table.Column<string>(type: "varchar(2)", nullable: false),
                    entered_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    exited_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_LIBERO_REPLACEMENT", x => x.match_libero_replacement_id);
                    table.ForeignKey(
                        name: "FK_MATCH_LIBERO_REPLACEMENT_MATCH_PLAYER_libero_match_player_id",
                        column: x => x.libero_match_player_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_PLAYER",
                        principalColumn: "match_player_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_LIBERO_REPLACEMENT_MATCH_PLAYER_replaced_match_player_id",
                        column: x => x.replaced_match_player_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_PLAYER",
                        principalColumn: "match_player_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_LIBERO_REPLACEMENT_MATCH_SET_match_set_id",
                        column: x => x.match_set_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SET",
                        principalColumn: "match_set_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_LIBERO_REPLACEMENT_MATCH_TEAM_match_team_id",
                        column: x => x.match_team_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_TEAM",
                        principalColumn: "match_team_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_LINEUP",
                schema: "dbo",
                columns: table => new
                {
                    match_lineup_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_set_id = table.Column<int>(type: "int", nullable: false),
                    match_team_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_LINEUP", x => x.match_lineup_id);
                    table.ForeignKey(
                        name: "FK_MATCH_LINEUP_MATCH_SET_match_set_id",
                        column: x => x.match_set_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SET",
                        principalColumn: "match_set_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_LINEUP_MATCH_TEAM_match_team_id",
                        column: x => x.match_team_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_TEAM",
                        principalColumn: "match_team_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_SUBSTITUTION",
                schema: "dbo",
                columns: table => new
                {
                    match_substitution_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    substitution_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_set_id = table.Column<int>(type: "int", nullable: false),
                    match_team_id = table.Column<int>(type: "int", nullable: false),
                    player_out_match_player_id = table.Column<int>(type: "int", nullable: false),
                    player_in_match_player_id = table.Column<int>(type: "int", nullable: false),
                    lineup_position = table.Column<string>(type: "varchar(2)", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_SUBSTITUTION", x => x.match_substitution_id);
                    table.ForeignKey(
                        name: "FK_MATCH_SUBSTITUTION_MATCH_PLAYER_player_in_match_player_id",
                        column: x => x.player_in_match_player_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_PLAYER",
                        principalColumn: "match_player_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_SUBSTITUTION_MATCH_PLAYER_player_out_match_player_id",
                        column: x => x.player_out_match_player_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_PLAYER",
                        principalColumn: "match_player_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_SUBSTITUTION_MATCH_SET_match_set_id",
                        column: x => x.match_set_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SET",
                        principalColumn: "match_set_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_SUBSTITUTION_MATCH_TEAM_match_team_id",
                        column: x => x.match_team_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_TEAM",
                        principalColumn: "match_team_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_TIMEOUT",
                schema: "dbo",
                columns: table => new
                {
                    match_timeout_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    timeout_uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    match_set_id = table.Column<int>(type: "int", nullable: false),
                    match_team_id = table.Column<int>(type: "int", nullable: false),
                    timeout_number = table.Column<byte>(type: "tinyint", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_TIMEOUT", x => x.match_timeout_id);
                    table.CheckConstraint("CK_MATCH_TIMEOUT_number", "[timeout_number] BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_MATCH_TIMEOUT_MATCH_SET_match_set_id",
                        column: x => x.match_set_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SET",
                        principalColumn: "match_set_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_TIMEOUT_MATCH_TEAM_match_team_id",
                        column: x => x.match_team_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_TEAM",
                        principalColumn: "match_team_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATCH_LINEUP_POSITION",
                schema: "dbo",
                columns: table => new
                {
                    match_lineup_position_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_lineup_id = table.Column<int>(type: "int", nullable: false),
                    position = table.Column<string>(type: "varchar(2)", nullable: false),
                    match_player_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_LINEUP_POSITION", x => x.match_lineup_position_id);
                    table.CheckConstraint("CK_MATCH_LINEUP_POSITION_position", "[position] IN ('P1','P2','P3','P4','P5','P6')");
                    table.ForeignKey(
                        name: "FK_MATCH_LINEUP_POSITION_MATCH_LINEUP_match_lineup_id",
                        column: x => x.match_lineup_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_LINEUP",
                        principalColumn: "match_lineup_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MATCH_LINEUP_POSITION_MATCH_PLAYER_match_player_id",
                        column: x => x.match_player_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_PLAYER",
                        principalColumn: "match_player_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_SET_sheet_number",
                schema: "dbo",
                table: "MATCH_SET",
                columns: new[] { "match_sheet_id", "set_number" },
                unique: true,
                filter: "[match_sheet_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_SET_uuid",
                schema: "dbo",
                table: "MATCH_SET",
                column: "set_uuid",
                unique: true,
                filter: "[set_uuid] <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "UX_MATCH_SET_active",
                schema: "dbo",
                table: "MATCH_SET",
                column: "match_sheet_id",
                unique: true,
                filter: "[match_sheet_id] IS NOT NULL AND [status] IN ('READY','IN_PROGRESS')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_SET_rotation",
                schema: "dbo",
                table: "MATCH_SET",
                sql: "[home_rotation_offset] BETWEEN 0 AND 5 AND [away_rotation_offset] BETWEEN 0 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_SET_sides",
                schema: "dbo",
                table: "MATCH_SET",
                sql: "([winner_side] IS NULL OR [winner_side] IN ('HOME','AWAY')) AND ([initial_serving_side] IS NULL OR [initial_serving_side] IN ('HOME','AWAY')) AND ([current_serving_side] IS NULL OR [current_serving_side] IN ('HOME','AWAY'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MATCH_SET_status",
                schema: "dbo",
                table: "MATCH_SET",
                sql: "[status] IN ('READY','IN_PROGRESS','FINISHED')");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_EVENT_match_player_id",
                schema: "dbo",
                table: "MATCH_EVENT",
                column: "match_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_EVENT_match_set_id",
                schema: "dbo",
                table: "MATCH_EVENT",
                column: "match_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_EVENT_related_event_id",
                schema: "dbo",
                table: "MATCH_EVENT",
                column: "related_event_id");

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_EVENT_sequence",
                schema: "dbo",
                table: "MATCH_EVENT",
                columns: new[] { "match_sheet_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_EVENT_uuid",
                schema: "dbo",
                table: "MATCH_EVENT",
                column: "event_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LIBERO_REPLACEMENT_libero_match_player_id",
                schema: "dbo",
                table: "MATCH_LIBERO_REPLACEMENT",
                column: "libero_match_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LIBERO_REPLACEMENT_match_team_id",
                schema: "dbo",
                table: "MATCH_LIBERO_REPLACEMENT",
                column: "match_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LIBERO_REPLACEMENT_replaced_match_player_id",
                schema: "dbo",
                table: "MATCH_LIBERO_REPLACEMENT",
                column: "replaced_match_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LIBERO_REPLACEMENT_replacement_uuid",
                schema: "dbo",
                table: "MATCH_LIBERO_REPLACEMENT",
                column: "replacement_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MATCH_LIBERO_REPLACEMENT_active",
                schema: "dbo",
                table: "MATCH_LIBERO_REPLACEMENT",
                columns: new[] { "match_set_id", "libero_match_player_id" },
                unique: true,
                filter: "[exited_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LINEUP_match_team_id",
                schema: "dbo",
                table: "MATCH_LINEUP",
                column: "match_team_id");

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_LINEUP_team",
                schema: "dbo",
                table: "MATCH_LINEUP",
                columns: new[] { "match_set_id", "match_team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LINEUP_POSITION_match_lineup_id_match_player_id",
                schema: "dbo",
                table: "MATCH_LINEUP_POSITION",
                columns: new[] { "match_lineup_id", "match_player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LINEUP_POSITION_match_lineup_id_position",
                schema: "dbo",
                table: "MATCH_LINEUP_POSITION",
                columns: new[] { "match_lineup_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_LINEUP_POSITION_match_player_id",
                schema: "dbo",
                table: "MATCH_LINEUP_POSITION",
                column: "match_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SUBSTITUTION_match_set_id",
                schema: "dbo",
                table: "MATCH_SUBSTITUTION",
                column: "match_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SUBSTITUTION_match_team_id",
                schema: "dbo",
                table: "MATCH_SUBSTITUTION",
                column: "match_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SUBSTITUTION_player_in_match_player_id",
                schema: "dbo",
                table: "MATCH_SUBSTITUTION",
                column: "player_in_match_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SUBSTITUTION_player_out_match_player_id",
                schema: "dbo",
                table: "MATCH_SUBSTITUTION",
                column: "player_out_match_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SUBSTITUTION_substitution_uuid",
                schema: "dbo",
                table: "MATCH_SUBSTITUTION",
                column: "substitution_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_TIMEOUT_match_team_id",
                schema: "dbo",
                table: "MATCH_TIMEOUT",
                column: "match_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_TIMEOUT_timeout_uuid",
                schema: "dbo",
                table: "MATCH_TIMEOUT",
                column: "timeout_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_TIMEOUT_number",
                schema: "dbo",
                table: "MATCH_TIMEOUT",
                columns: new[] { "match_set_id", "match_team_id", "timeout_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MATCH_SET_MATCH_SHEET",
                schema: "dbo",
                table: "MATCH_SET",
                column: "match_sheet_id",
                principalSchema: "dbo",
                principalTable: "MATCH_SHEET",
                principalColumn: "match_sheet_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MATCH_SET_MATCH_SHEET",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropTable(
                name: "MATCH_EVENT",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_LIBERO_REPLACEMENT",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_LINEUP_POSITION",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_SUBSTITUTION",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_TIMEOUT",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MATCH_LINEUP",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UQ_MATCH_SET_sheet_number",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropIndex(
                name: "UQ_MATCH_SET_uuid",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropIndex(
                name: "UX_MATCH_SET_active",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_SET_rotation",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_SET_sides",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MATCH_SET_status",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropColumn(
                name: "track_libero_replacements",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropColumn(
                name: "track_substitutions",
                schema: "dbo",
                table: "MATCH_SHEET");

            migrationBuilder.DropColumn(
                name: "away_rotation_offset",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropColumn(
                name: "current_serving_side",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropColumn(
                name: "finished_at",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropColumn(
                name: "home_rotation_offset",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropColumn(
                name: "initial_serving_side",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropColumn(
                name: "match_sheet_id",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropColumn(
                name: "set_uuid",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropColumn(
                name: "started_at",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "dbo",
                table: "MATCH_SET");

            migrationBuilder.DropColumn(
                name: "winner_side",
                schema: "dbo",
                table: "MATCH_SET");
        }
    }
}
