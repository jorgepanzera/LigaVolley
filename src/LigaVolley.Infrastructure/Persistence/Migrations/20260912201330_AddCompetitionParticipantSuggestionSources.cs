using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionParticipantSuggestionSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COMPETITION_PARTICIPANT_SUGGESTION_SOURCE",
                schema: "dbo",
                columns: table => new
                {
                    competition_id = table.Column<int>(type: "int", nullable: false),
                    source_competition_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPETITION_PARTICIPANT_SUGGESTION_SOURCE", x => new { x.competition_id, x.source_competition_id });
                    table.CheckConstraint("CK_COMPETITION_PARTICIPANT_SUGGESTION_SOURCE_not_self", "[competition_id] <> [source_competition_id]");
                    table.ForeignKey(
                        name: "FK_COMPETITION_PARTICIPANT_SUGGESTION_SOURCE_COMPETITION_competition_id",
                        column: x => x.competition_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION",
                        principalColumn: "competition_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPETITION_PARTICIPANT_SUGGESTION_SOURCE_SOURCE",
                        column: x => x.source_competition_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION",
                        principalColumn: "competition_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COMPETITION_PARTICIPANT_SUGGESTION_SOURCE_source",
                schema: "dbo",
                table: "COMPETITION_PARTICIPANT_SUGGESTION_SOURCE",
                column: "source_competition_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COMPETITION_PARTICIPANT_SUGGESTION_SOURCE",
                schema: "dbo");
        }
    }
}
