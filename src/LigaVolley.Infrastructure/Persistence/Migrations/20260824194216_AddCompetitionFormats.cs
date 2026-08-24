using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionFormats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COMPETITION_FORMAT",
                schema: "dbo",
                columns: table => new
                {
                    competition_format_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "varchar(30)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    min_teams = table.Column<short>(type: "smallint", nullable: false),
                    max_teams = table.Column<short>(type: "smallint", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPETITION_FORMAT", x => x.competition_format_id);
                    table.CheckConstraint("CK_COMPETITION_FORMAT_team_range", "[min_teams] > 1 AND [max_teams] >= [min_teams]");
                });

            migrationBuilder.CreateTable(
                name: "FORMAT_PHASE",
                schema: "dbo",
                columns: table => new
                {
                    format_phase_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_format_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "varchar(30)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    phase_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    phase_role = table.Column<string>(type: "varchar(20)", nullable: false),
                    sequence = table.Column<short>(type: "smallint", nullable: false),
                    rounds = table.Column<short>(type: "smallint", nullable: true),
                    fixture_mode = table.Column<string>(type: "varchar(30)", nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FORMAT_PHASE", x => x.format_phase_id);
                    table.UniqueConstraint("UQ_FORMAT_PHASE_id_format", x => new { x.format_phase_id, x.competition_format_id });
                    table.CheckConstraint("CK_FORMAT_PHASE_fixture_mode", "[fixture_mode] IS NULL OR [fixture_mode] IN ('MIRRORED_HOME_AWAY','BALANCED_RANDOM','PLAYOFF')");
                    table.CheckConstraint("CK_FORMAT_PHASE_role", "[phase_role] IN ('REGULAR','CHAMPIONSHIP','RELEGATION','SEMIFINAL','THIRD_PLACE','FINAL')");
                    table.CheckConstraint("CK_FORMAT_PHASE_round_robin", "[phase_type] <> 'ROUND_ROBIN' OR ([rounds] IS NOT NULL AND [fixture_mode] IS NOT NULL)");
                    table.CheckConstraint("CK_FORMAT_PHASE_rounds", "[rounds] IS NULL OR [rounds] > 0");
                    table.CheckConstraint("CK_FORMAT_PHASE_sequence", "[sequence] > 0");
                    table.CheckConstraint("CK_FORMAT_PHASE_type", "[phase_type] IN ('ROUND_ROBIN','GROUP_STAGE','PLAYOFF')");
                    table.ForeignKey(
                        name: "FK_FORMAT_PHASE_COMPETITION_FORMAT_competition_format_id",
                        column: x => x.competition_format_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_FORMAT",
                        principalColumn: "competition_format_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FORMAT_SCORING_RULE",
                schema: "dbo",
                columns: table => new
                {
                    format_scoring_rule_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_format_id = table.Column<int>(type: "int", nullable: false),
                    winner_sets = table.Column<byte>(type: "tinyint", nullable: false),
                    loser_sets = table.Column<byte>(type: "tinyint", nullable: false),
                    winner_table_points = table.Column<short>(type: "smallint", nullable: false),
                    loser_table_points = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FORMAT_SCORING_RULE", x => x.format_scoring_rule_id);
                    table.ForeignKey(
                        name: "FK_FORMAT_SCORING_RULE_COMPETITION_FORMAT_competition_format_id",
                        column: x => x.competition_format_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_FORMAT",
                        principalColumn: "competition_format_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FORMAT_TIEBREAK_RULE",
                schema: "dbo",
                columns: table => new
                {
                    format_tiebreak_rule_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_format_id = table.Column<int>(type: "int", nullable: false),
                    sequence = table.Column<short>(type: "smallint", nullable: false),
                    criterion = table.Column<string>(type: "varchar(30)", nullable: false),
                    sort_direction = table.Column<string>(type: "varchar(4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FORMAT_TIEBREAK_RULE", x => x.format_tiebreak_rule_id);
                    table.ForeignKey(
                        name: "FK_FORMAT_TIEBREAK_RULE_COMPETITION_FORMAT_competition_format_id",
                        column: x => x.competition_format_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_FORMAT",
                        principalColumn: "competition_format_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FORMAT_GROUP",
                schema: "dbo",
                columns: table => new
                {
                    format_group_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_format_id = table.Column<int>(type: "int", nullable: false),
                    format_phase_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "varchar(30)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    group_role = table.Column<string>(type: "varchar(20)", nullable: false),
                    sequence = table.Column<short>(type: "smallint", nullable: false),
                    rounds = table.Column<short>(type: "smallint", nullable: false),
                    fixture_mode = table.Column<string>(type: "varchar(30)", nullable: false),
                    carry_over_mode = table.Column<string>(type: "varchar(20)", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FORMAT_GROUP", x => x.format_group_id);
                    table.UniqueConstraint("UQ_FORMAT_GROUP_id_format", x => new { x.format_group_id, x.competition_format_id });
                    table.CheckConstraint("CK_FORMAT_GROUP_carry_over", "[carry_over_mode] IN ('NONE','ALL','QUALIFIED_ONLY')");
                    table.CheckConstraint("CK_FORMAT_GROUP_fixture_mode", "[fixture_mode] IN ('MIRRORED_HOME_AWAY','BALANCED_RANDOM')");
                    table.CheckConstraint("CK_FORMAT_GROUP_role", "[group_role] IN ('CHAMPIONSHIP','RELEGATION','OTHER')");
                    table.CheckConstraint("CK_FORMAT_GROUP_rounds", "[rounds] > 0");
                    table.CheckConstraint("CK_FORMAT_GROUP_sequence", "[sequence] > 0");
                    table.ForeignKey(
                        name: "FK_FORMAT_GROUP_FORMAT_PHASE_format_phase_id_competition_format_id",
                        columns: x => new { x.format_phase_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PHASE",
                        principalColumns: new[] { "format_phase_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FORMAT_PLAYOFF_SERIES",
                schema: "dbo",
                columns: table => new
                {
                    format_series_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_format_id = table.Column<int>(type: "int", nullable: false),
                    format_phase_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "varchar(30)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    sequence = table.Column<short>(type: "smallint", nullable: false),
                    wins_required = table.Column<short>(type: "smallint", nullable: false),
                    team1_initial_wins = table.Column<short>(type: "smallint", nullable: false),
                    team2_initial_wins = table.Column<short>(type: "smallint", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FORMAT_PLAYOFF_SERIES", x => x.format_series_id);
                    table.UniqueConstraint("UQ_FORMAT_PLAYOFF_SERIES_id_format", x => new { x.format_series_id, x.competition_format_id });
                    table.CheckConstraint("CK_FORMAT_PLAYOFF_SERIES_initial_wins", "[team1_initial_wins] >= 0 AND [team2_initial_wins] >= 0 AND [team1_initial_wins] < [wins_required] AND [team2_initial_wins] < [wins_required]");
                    table.CheckConstraint("CK_FORMAT_PLAYOFF_SERIES_sequence", "[sequence] > 0");
                    table.CheckConstraint("CK_FORMAT_PLAYOFF_SERIES_wins_required", "[wins_required] > 0");
                    table.ForeignKey(
                        name: "FK_FORMAT_PLAYOFF_SERIES_FORMAT_PHASE_format_phase_id_competition_format_id",
                        columns: x => new { x.format_phase_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PHASE",
                        principalColumns: new[] { "format_phase_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FORMAT_MOVEMENT_RULE",
                schema: "dbo",
                columns: table => new
                {
                    format_movement_rule_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_format_id = table.Column<int>(type: "int", nullable: false),
                    movement_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    source_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    source_format_phase_id = table.Column<int>(type: "int", nullable: true),
                    source_format_group_id = table.Column<int>(type: "int", nullable: true),
                    source_format_series_id = table.Column<int>(type: "int", nullable: true),
                    from_position = table.Column<short>(type: "smallint", nullable: false),
                    to_position = table.Column<short>(type: "smallint", nullable: false),
                    target_level_delta = table.Column<short>(type: "smallint", nullable: false),
                    applies_if_target_exists = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FORMAT_MOVEMENT_RULE", x => x.format_movement_rule_id);
                    table.ForeignKey(
                        name: "FK_FORMAT_MOVEMENT_RULE_COMPETITION_FORMAT_competition_format_id",
                        column: x => x.competition_format_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_FORMAT",
                        principalColumn: "competition_format_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FORMAT_MOVEMENT_RULE_FORMAT_GROUP_source_format_group_id_competition_format_id",
                        columns: x => new { x.source_format_group_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_GROUP",
                        principalColumns: new[] { "format_group_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FORMAT_MOVEMENT_RULE_FORMAT_PHASE_source_format_phase_id_competition_format_id",
                        columns: x => new { x.source_format_phase_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PHASE",
                        principalColumns: new[] { "format_phase_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FORMAT_MOVEMENT_RULE_FORMAT_PLAYOFF_SERIES_source_format_series_id_competition_format_id",
                        columns: x => new { x.source_format_series_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PLAYOFF_SERIES",
                        principalColumns: new[] { "format_series_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FORMAT_QUALIFICATION_RULE",
                schema: "dbo",
                columns: table => new
                {
                    qualification_rule_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_format_id = table.Column<int>(type: "int", nullable: false),
                    source_format_phase_id = table.Column<int>(type: "int", nullable: false),
                    source_format_group_id = table.Column<int>(type: "int", nullable: true),
                    selection_mode = table.Column<string>(type: "varchar(30)", nullable: false),
                    from_position = table.Column<short>(type: "smallint", nullable: true),
                    to_position = table.Column<short>(type: "smallint", nullable: true),
                    target_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    target_format_phase_id = table.Column<int>(type: "int", nullable: true),
                    target_format_group_id = table.Column<int>(type: "int", nullable: true),
                    target_format_series_id = table.Column<int>(type: "int", nullable: true),
                    target_side = table.Column<byte>(type: "tinyint", nullable: true),
                    sequence = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FORMAT_QUALIFICATION_RULE", x => x.qualification_rule_id);
                    table.ForeignKey(
                        name: "FK_FORMAT_QUALIFICATION_RULE_COMPETITION_FORMAT_competition_format_id",
                        column: x => x.competition_format_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_FORMAT",
                        principalColumn: "competition_format_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FORMAT_QUALIFICATION_RULE_FORMAT_GROUP_source_format_group_id_competition_format_id",
                        columns: x => new { x.source_format_group_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_GROUP",
                        principalColumns: new[] { "format_group_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FORMAT_QUALIFICATION_RULE_FORMAT_GROUP_target_format_group_id_competition_format_id",
                        columns: x => new { x.target_format_group_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_GROUP",
                        principalColumns: new[] { "format_group_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FORMAT_QUALIFICATION_RULE_FORMAT_PHASE_source_format_phase_id_competition_format_id",
                        columns: x => new { x.source_format_phase_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PHASE",
                        principalColumns: new[] { "format_phase_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FORMAT_QUALIFICATION_RULE_FORMAT_PHASE_target_format_phase_id_competition_format_id",
                        columns: x => new { x.target_format_phase_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PHASE",
                        principalColumns: new[] { "format_phase_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FORMAT_QUALIFICATION_RULE_FORMAT_PLAYOFF_SERIES_target_format_series_id_competition_format_id",
                        columns: x => new { x.target_format_series_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PLAYOFF_SERIES",
                        principalColumns: new[] { "format_series_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FORMAT_SERIES_PARTICIPANT_SOURCE",
                schema: "dbo",
                columns: table => new
                {
                    format_series_participant_source_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_format_id = table.Column<int>(type: "int", nullable: false),
                    target_format_series_id = table.Column<int>(type: "int", nullable: false),
                    target_side = table.Column<byte>(type: "tinyint", nullable: false),
                    source_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    source_format_series_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FORMAT_SERIES_PARTICIPANT_SOURCE", x => x.format_series_participant_source_id);
                    table.CheckConstraint("CK_FORMAT_SERIES_SOURCE_not_same", "[target_format_series_id] <> [source_format_series_id]");
                    table.CheckConstraint("CK_FORMAT_SERIES_SOURCE_side", "[target_side] IN (1,2)");
                    table.CheckConstraint("CK_FORMAT_SERIES_SOURCE_type", "[source_type] IN ('SERIES_WINNER','SERIES_LOSER')");
                    table.ForeignKey(
                        name: "FK_FORMAT_SERIES_PARTICIPANT_SOURCE_FORMAT_PLAYOFF_SERIES_source_format_series_id_competition_format_id",
                        columns: x => new { x.source_format_series_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PLAYOFF_SERIES",
                        principalColumns: new[] { "format_series_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FORMAT_SERIES_PARTICIPANT_SOURCE_FORMAT_PLAYOFF_SERIES_target_format_series_id_competition_format_id",
                        columns: x => new { x.target_format_series_id, x.competition_format_id },
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PLAYOFF_SERIES",
                        principalColumns: new[] { "format_series_id", "competition_format_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_COMPETITION_FORMAT_code",
                schema: "dbo",
                table: "COMPETITION_FORMAT",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_GROUP_format_phase_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_GROUP",
                columns: new[] { "format_phase_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_FORMAT_GROUP_code",
                schema: "dbo",
                table: "FORMAT_GROUP",
                columns: new[] { "format_phase_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_MOVEMENT_RULE_competition_format_id",
                schema: "dbo",
                table: "FORMAT_MOVEMENT_RULE",
                column: "competition_format_id");

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_MOVEMENT_RULE_source_format_group_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_MOVEMENT_RULE",
                columns: new[] { "source_format_group_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_MOVEMENT_RULE_source_format_phase_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_MOVEMENT_RULE",
                columns: new[] { "source_format_phase_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_MOVEMENT_RULE_source_format_series_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_MOVEMENT_RULE",
                columns: new[] { "source_format_series_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_FORMAT_PHASE_code",
                schema: "dbo",
                table: "FORMAT_PHASE",
                columns: new[] { "competition_format_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_PLAYOFF_SERIES_format_phase_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_PLAYOFF_SERIES",
                columns: new[] { "format_phase_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_FORMAT_PLAYOFF_SERIES_format_code",
                schema: "dbo",
                table: "FORMAT_PLAYOFF_SERIES",
                columns: new[] { "competition_format_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_QUALIFICATION_RULE_competition_format_id",
                schema: "dbo",
                table: "FORMAT_QUALIFICATION_RULE",
                column: "competition_format_id");

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_QUALIFICATION_RULE_source_format_group_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_QUALIFICATION_RULE",
                columns: new[] { "source_format_group_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_QUALIFICATION_RULE_source_format_phase_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_QUALIFICATION_RULE",
                columns: new[] { "source_format_phase_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_QUALIFICATION_RULE_target_format_group_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_QUALIFICATION_RULE",
                columns: new[] { "target_format_group_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_QUALIFICATION_RULE_target_format_phase_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_QUALIFICATION_RULE",
                columns: new[] { "target_format_phase_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_QUALIFICATION_RULE_target_format_series_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_QUALIFICATION_RULE",
                columns: new[] { "target_format_series_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_FORMAT_SCORING_RULE_score",
                schema: "dbo",
                table: "FORMAT_SCORING_RULE",
                columns: new[] { "competition_format_id", "winner_sets", "loser_sets" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_SERIES_PARTICIPANT_SOURCE_source_format_series_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_SERIES_PARTICIPANT_SOURCE",
                columns: new[] { "source_format_series_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "IX_FORMAT_SERIES_PARTICIPANT_SOURCE_target_format_series_id_competition_format_id",
                schema: "dbo",
                table: "FORMAT_SERIES_PARTICIPANT_SOURCE",
                columns: new[] { "target_format_series_id", "competition_format_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_FORMAT_SERIES_SOURCE_target_side",
                schema: "dbo",
                table: "FORMAT_SERIES_PARTICIPANT_SOURCE",
                columns: new[] { "target_format_series_id", "target_side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_FORMAT_TIEBREAK_RULE_sequence",
                schema: "dbo",
                table: "FORMAT_TIEBREAK_RULE",
                columns: new[] { "competition_format_id", "sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FORMAT_MOVEMENT_RULE",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FORMAT_QUALIFICATION_RULE",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FORMAT_SCORING_RULE",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FORMAT_SERIES_PARTICIPANT_SOURCE",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FORMAT_TIEBREAK_RULE",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FORMAT_GROUP",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FORMAT_PLAYOFF_SERIES",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FORMAT_PHASE",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COMPETITION_FORMAT",
                schema: "dbo");
        }
    }
}
