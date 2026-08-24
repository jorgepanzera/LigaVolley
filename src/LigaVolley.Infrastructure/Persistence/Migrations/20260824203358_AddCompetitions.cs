using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COMPETITION",
                schema: "dbo",
                columns: table => new
                {
                    competition_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    season_id = table.Column<int>(type: "int", nullable: false),
                    division_id = table.Column<int>(type: "int", nullable: false),
                    competition_format_id = table.Column<int>(type: "int", nullable: false),
                    period_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPETITION", x => x.competition_id);
                    table.CheckConstraint("CK_COMPETITION_dates", "[end_date] IS NULL OR [start_date] IS NULL OR [end_date] >= [start_date]");
                    table.CheckConstraint("CK_COMPETITION_status", "[status] IN ('DRAFT','SCHEDULED','IN_PROGRESS','FINISHED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_COMPETITION_COMPETITION_FORMAT_competition_format_id",
                        column: x => x.competition_format_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_FORMAT",
                        principalColumn: "competition_format_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPETITION_DIVISION_division_id",
                        column: x => x.division_id,
                        principalSchema: "dbo",
                        principalTable: "DIVISION",
                        principalColumn: "division_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPETITION_SEASON_season_id",
                        column: x => x.season_id,
                        principalSchema: "dbo",
                        principalTable: "SEASON",
                        principalColumn: "season_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COMPETITION_PHASE",
                schema: "dbo",
                columns: table => new
                {
                    competition_phase_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_id = table.Column<int>(type: "int", nullable: false),
                    format_phase_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "varchar(30)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    phase_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    phase_role = table.Column<string>(type: "varchar(20)", nullable: false),
                    sequence = table.Column<short>(type: "smallint", nullable: false),
                    rounds = table.Column<short>(type: "smallint", nullable: true),
                    fixture_mode = table.Column<string>(type: "varchar(30)", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPETITION_PHASE", x => x.competition_phase_id);
                    table.ForeignKey(
                        name: "FK_COMPETITION_PHASE_COMPETITION_competition_id",
                        column: x => x.competition_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION",
                        principalColumn: "competition_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPETITION_PHASE_FORMAT_PHASE_format_phase_id",
                        column: x => x.format_phase_id,
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PHASE",
                        principalColumn: "format_phase_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PHASE_GROUP",
                schema: "dbo",
                columns: table => new
                {
                    phase_group_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_phase_id = table.Column<int>(type: "int", nullable: false),
                    format_group_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "varchar(30)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    group_role = table.Column<string>(type: "varchar(20)", nullable: false),
                    sequence = table.Column<short>(type: "smallint", nullable: false),
                    rounds = table.Column<short>(type: "smallint", nullable: false),
                    fixture_mode = table.Column<string>(type: "varchar(30)", nullable: false),
                    carry_over_mode = table.Column<string>(type: "varchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PHASE_GROUP", x => x.phase_group_id);
                    table.ForeignKey(
                        name: "FK_PHASE_GROUP_COMPETITION_PHASE_competition_phase_id",
                        column: x => x.competition_phase_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_PHASE",
                        principalColumn: "competition_phase_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PHASE_GROUP_FORMAT_GROUP_format_group_id",
                        column: x => x.format_group_id,
                        principalSchema: "dbo",
                        principalTable: "FORMAT_GROUP",
                        principalColumn: "format_group_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PLAYOFF_SERIES",
                schema: "dbo",
                columns: table => new
                {
                    playoff_series_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_phase_id = table.Column<int>(type: "int", nullable: false),
                    format_series_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "varchar(30)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    sequence = table.Column<short>(type: "smallint", nullable: false),
                    wins_required = table.Column<short>(type: "smallint", nullable: false),
                    team1_initial_wins = table.Column<short>(type: "smallint", nullable: false),
                    team2_initial_wins = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PLAYOFF_SERIES", x => x.playoff_series_id);
                    table.ForeignKey(
                        name: "FK_PLAYOFF_SERIES_COMPETITION_PHASE_competition_phase_id",
                        column: x => x.competition_phase_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION_PHASE",
                        principalColumn: "competition_phase_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PLAYOFF_SERIES_FORMAT_PLAYOFF_SERIES_format_series_id",
                        column: x => x.format_series_id,
                        principalSchema: "dbo",
                        principalTable: "FORMAT_PLAYOFF_SERIES",
                        principalColumn: "format_series_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SERIES_PARTICIPANT_SOURCE",
                schema: "dbo",
                columns: table => new
                {
                    series_participant_source_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    target_playoff_series_id = table.Column<int>(type: "int", nullable: false),
                    target_side = table.Column<byte>(type: "tinyint", nullable: false),
                    source_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    source_playoff_series_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SERIES_PARTICIPANT_SOURCE", x => x.series_participant_source_id);
                    table.CheckConstraint("CK_SERIES_PARTICIPANT_SOURCE_side", "[target_side] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_SERIES_PARTICIPANT_SOURCE_PLAYOFF_SERIES_source_playoff_series_id",
                        column: x => x.source_playoff_series_id,
                        principalSchema: "dbo",
                        principalTable: "PLAYOFF_SERIES",
                        principalColumn: "playoff_series_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SERIES_PARTICIPANT_SOURCE_PLAYOFF_SERIES_target_playoff_series_id",
                        column: x => x.target_playoff_series_id,
                        principalSchema: "dbo",
                        principalTable: "PLAYOFF_SERIES",
                        principalColumn: "playoff_series_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_competition_format_id",
                schema: "dbo",
                table: "COMPETITION",
                column: "competition_format_id");

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_division_id",
                schema: "dbo",
                table: "COMPETITION",
                column: "division_id");

            migrationBuilder.CreateIndex(
                name: "UQ_COMPETITION_season_division_name",
                schema: "dbo",
                table: "COMPETITION",
                columns: new[] { "season_id", "division_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_PHASE_competition_id_code",
                schema: "dbo",
                table: "COMPETITION_PHASE",
                columns: new[] { "competition_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_PHASE_format_phase_id",
                schema: "dbo",
                table: "COMPETITION_PHASE",
                column: "format_phase_id");

            migrationBuilder.CreateIndex(
                name: "IX_PHASE_GROUP_competition_phase_id_code",
                schema: "dbo",
                table: "PHASE_GROUP",
                columns: new[] { "competition_phase_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PHASE_GROUP_format_group_id",
                schema: "dbo",
                table: "PHASE_GROUP",
                column: "format_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_PLAYOFF_SERIES_competition_phase_id_code",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                columns: new[] { "competition_phase_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PLAYOFF_SERIES_format_series_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                column: "format_series_id");

            migrationBuilder.CreateIndex(
                name: "IX_SERIES_PARTICIPANT_SOURCE_source_playoff_series_id",
                schema: "dbo",
                table: "SERIES_PARTICIPANT_SOURCE",
                column: "source_playoff_series_id");

            migrationBuilder.CreateIndex(
                name: "IX_SERIES_PARTICIPANT_SOURCE_target_playoff_series_id_target_side",
                schema: "dbo",
                table: "SERIES_PARTICIPANT_SOURCE",
                columns: new[] { "target_playoff_series_id", "target_side" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PHASE_GROUP",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SERIES_PARTICIPANT_SOURCE",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PLAYOFF_SERIES",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COMPETITION_PHASE",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COMPETITION",
                schema: "dbo");
        }
    }
}
