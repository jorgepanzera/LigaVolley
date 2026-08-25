using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchSetLiberoPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MATCH_SET_LIBERO_PLAN",
                schema: "dbo",
                columns: table => new
                {
                    match_set_libero_plan_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_set_id = table.Column<int>(type: "int", nullable: false),
                    match_team_id = table.Column<int>(type: "int", nullable: false),
                    libero_match_player_id = table.Column<int>(type: "int", nullable: false),
                    logical_positions_mask = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATCH_SET_LIBERO_PLAN", x => x.match_set_libero_plan_id);
                    table.CheckConstraint("CK_MATCH_SET_LIBERO_PLAN_mask", "[logical_positions_mask] BETWEEN 1 AND 63");
                    table.ForeignKey(
                        name: "FK_MATCH_SET_LIBERO_PLAN_MATCH_PLAYER_libero_match_player_id",
                        column: x => x.libero_match_player_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_PLAYER",
                        principalColumn: "match_player_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MATCH_SET_LIBERO_PLAN_MATCH_SET_match_set_id",
                        column: x => x.match_set_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_SET",
                        principalColumn: "match_set_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MATCH_SET_LIBERO_PLAN_MATCH_TEAM_match_team_id",
                        column: x => x.match_team_id,
                        principalSchema: "dbo",
                        principalTable: "MATCH_TEAM",
                        principalColumn: "match_team_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SET_LIBERO_PLAN_libero_match_player_id",
                schema: "dbo",
                table: "MATCH_SET_LIBERO_PLAN",
                column: "libero_match_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_MATCH_SET_LIBERO_PLAN_match_team_id",
                schema: "dbo",
                table: "MATCH_SET_LIBERO_PLAN",
                column: "match_team_id");

            migrationBuilder.CreateIndex(
                name: "UQ_MATCH_SET_LIBERO_PLAN_team",
                schema: "dbo",
                table: "MATCH_SET_LIBERO_PLAN",
                columns: new[] { "match_set_id", "match_team_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MATCH_SET_LIBERO_PLAN",
                schema: "dbo");
        }
    }
}
