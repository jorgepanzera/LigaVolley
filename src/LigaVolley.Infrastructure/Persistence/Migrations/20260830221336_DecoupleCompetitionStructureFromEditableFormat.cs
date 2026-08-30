using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleCompetitionStructureFromEditableFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_COMPETITION_PHASE_FORMAT_PHASE_format_phase_id",
                schema: "dbo",
                table: "COMPETITION_PHASE");

            migrationBuilder.DropForeignKey(
                name: "FK_PHASE_GROUP_FORMAT_GROUP_format_group_id",
                schema: "dbo",
                table: "PHASE_GROUP");

            migrationBuilder.DropForeignKey(
                name: "FK_PLAYOFF_SERIES_FORMAT_PLAYOFF_SERIES_format_series_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.AlterColumn<int>(
                name: "format_series_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "format_group_id",
                schema: "dbo",
                table: "PHASE_GROUP",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "format_phase_id",
                schema: "dbo",
                table: "COMPETITION_PHASE",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_COMPETITION_PHASE_FORMAT_PHASE_format_phase_id",
                schema: "dbo",
                table: "COMPETITION_PHASE",
                column: "format_phase_id",
                principalSchema: "dbo",
                principalTable: "FORMAT_PHASE",
                principalColumn: "format_phase_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PHASE_GROUP_FORMAT_GROUP_format_group_id",
                schema: "dbo",
                table: "PHASE_GROUP",
                column: "format_group_id",
                principalSchema: "dbo",
                principalTable: "FORMAT_GROUP",
                principalColumn: "format_group_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PLAYOFF_SERIES_FORMAT_PLAYOFF_SERIES_format_series_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                column: "format_series_id",
                principalSchema: "dbo",
                principalTable: "FORMAT_PLAYOFF_SERIES",
                principalColumn: "format_series_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_COMPETITION_PHASE_FORMAT_PHASE_format_phase_id",
                schema: "dbo",
                table: "COMPETITION_PHASE");

            migrationBuilder.DropForeignKey(
                name: "FK_PHASE_GROUP_FORMAT_GROUP_format_group_id",
                schema: "dbo",
                table: "PHASE_GROUP");

            migrationBuilder.DropForeignKey(
                name: "FK_PLAYOFF_SERIES_FORMAT_PLAYOFF_SERIES_format_series_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES");

            migrationBuilder.AlterColumn<int>(
                name: "format_series_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "format_group_id",
                schema: "dbo",
                table: "PHASE_GROUP",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "format_phase_id",
                schema: "dbo",
                table: "COMPETITION_PHASE",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_COMPETITION_PHASE_FORMAT_PHASE_format_phase_id",
                schema: "dbo",
                table: "COMPETITION_PHASE",
                column: "format_phase_id",
                principalSchema: "dbo",
                principalTable: "FORMAT_PHASE",
                principalColumn: "format_phase_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PHASE_GROUP_FORMAT_GROUP_format_group_id",
                schema: "dbo",
                table: "PHASE_GROUP",
                column: "format_group_id",
                principalSchema: "dbo",
                principalTable: "FORMAT_GROUP",
                principalColumn: "format_group_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PLAYOFF_SERIES_FORMAT_PLAYOFF_SERIES_format_series_id",
                schema: "dbo",
                table: "PLAYOFF_SERIES",
                column: "format_series_id",
                principalSchema: "dbo",
                principalTable: "FORMAT_PLAYOFF_SERIES",
                principalColumn: "format_series_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
