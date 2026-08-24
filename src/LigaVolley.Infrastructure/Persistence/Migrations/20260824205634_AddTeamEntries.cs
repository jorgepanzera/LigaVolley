using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TEAM_ENTRY",
                schema: "dbo",
                columns: table => new
                {
                    team_entry_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_id = table.Column<int>(type: "int", nullable: false),
                    team_id = table.Column<int>(type: "int", nullable: false),
                    seed = table.Column<short>(type: "smallint", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "REGISTERED")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TEAM_ENTRY", x => x.team_entry_id);
                    table.UniqueConstraint("UQ_TEAM_ENTRY_id_comp", x => new { x.team_entry_id, x.competition_id });
                    table.CheckConstraint("CK_TEAM_ENTRY_seed", "[seed] IS NULL OR [seed] > 0");
                    table.CheckConstraint("CK_TEAM_ENTRY_status", "[status] IN ('REGISTERED','ACTIVE','WITHDRAWN','DISQUALIFIED')");
                    table.ForeignKey(
                        name: "FK_TEAM_ENTRY_COMPETITION",
                        column: x => x.competition_id,
                        principalSchema: "dbo",
                        principalTable: "COMPETITION",
                        principalColumn: "competition_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TEAM_ENTRY_TEAM",
                        column: x => x.team_id,
                        principalSchema: "dbo",
                        principalTable: "TEAM",
                        principalColumn: "team_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TEAM_ENTRY_competition",
                schema: "dbo",
                table: "TEAM_ENTRY",
                columns: new[] { "competition_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_TEAM_ENTRY_team_id",
                schema: "dbo",
                table: "TEAM_ENTRY",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "UQ_TEAM_ENTRY",
                schema: "dbo",
                table: "TEAM_ENTRY",
                columns: new[] { "competition_id", "team_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TEAM_ENTRY",
                schema: "dbo");
        }
    }
}
