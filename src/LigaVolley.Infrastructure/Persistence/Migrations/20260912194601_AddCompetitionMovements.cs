using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COMPETITION_MOVEMENT",
                schema: "dbo",
                columns: table => new
                {
                    competition_movement_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_id = table.Column<int>(type: "int", nullable: false),
                    movement_rule_id = table.Column<int>(type: "int", nullable: false),
                    movement_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    team_entry_id = table.Column<int>(type: "int", nullable: false),
                    team_id = table.Column<int>(type: "int", nullable: false),
                    source_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    source_phase_id = table.Column<int>(type: "int", nullable: false),
                    source_phase_group_id = table.Column<int>(type: "int", nullable: true),
                    source_series_id = table.Column<int>(type: "int", nullable: true),
                    source_position = table.Column<short>(type: "smallint", nullable: false),
                    standing_position = table.Column<short>(type: "smallint", nullable: true),
                    source_division_id = table.Column<int>(type: "int", nullable: false),
                    target_division_id = table.Column<int>(type: "int", nullable: false),
                    target_level_delta = table.Column<short>(type: "smallint", nullable: false),
                    achieved_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPETITION_MOVEMENT", x => x.competition_movement_id);
                    table.CheckConstraint("CK_COMPETITION_MOVEMENT_source_position", "[source_position] > 0");
                    table.CheckConstraint("CK_COMPETITION_MOVEMENT_standing_position", "[standing_position] IS NULL OR [standing_position] > 0");
                    table.ForeignKey(
                        name: "FK_COMPETITION_MOVEMENT_COMPETITION_competition_id",
                        column: x => x.competition_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION",
                        principalColumn: "competition_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPETITION_MOVEMENT_DIVISION_source_division_id",
                        column: x => x.source_division_id,
                        principalSchema: "dbo",
                        principalTable: "DIVISION",
                        principalColumn: "division_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPETITION_MOVEMENT_DIVISION_target_division_id",
                        column: x => x.target_division_id,
                        principalSchema: "dbo",
                        principalTable: "DIVISION",
                        principalColumn: "division_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPETITION_MOVEMENT_TEAM_ENTRY_team_entry_id_competition_id",
                        columns: x => new { x.team_entry_id, x.competition_id },
                        principalSchema: "dbo",
                        principalTable: "TEAM_ENTRY",
                        principalColumns: new[] { "team_entry_id", "competition_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_MOVEMENT_competition",
                schema: "dbo",
                table: "COMPETITION_MOVEMENT",
                column: "competition_id");

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_MOVEMENT_source_division_id",
                schema: "dbo",
                table: "COMPETITION_MOVEMENT",
                column: "source_division_id");

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_MOVEMENT_target_division_id",
                schema: "dbo",
                table: "COMPETITION_MOVEMENT",
                column: "target_division_id");

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_MOVEMENT_team_entry_id_competition_id",
                schema: "dbo",
                table: "COMPETITION_MOVEMENT",
                columns: new[] { "team_entry_id", "competition_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_COMPETITION_MOVEMENT_rule_team",
                schema: "dbo",
                table: "COMPETITION_MOVEMENT",
                columns: new[] { "competition_id", "movement_rule_id", "team_entry_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COMPETITION_MOVEMENT",
                schema: "dbo");
        }
    }
}
