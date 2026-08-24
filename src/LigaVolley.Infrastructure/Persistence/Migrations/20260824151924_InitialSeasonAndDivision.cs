using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSeasonAndDivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "DIVISION",
                schema: "dbo",
                columns: table => new
                {
                    division_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    level_order = table.Column<short>(type: "smallint", nullable: false),
                    gender = table.Column<string>(type: "char(1)", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DIVISION", x => x.division_id);
                    table.UniqueConstraint("UQ_DIVISION_level_gender", x => new { x.level_order, x.gender });
                    table.UniqueConstraint("UQ_DIVISION_name_gender", x => new { x.name, x.gender });
                    table.CheckConstraint("CK_DIVISION_gender", "[gender] IN ('M','F')");
                    table.CheckConstraint("CK_DIVISION_level_order", "[level_order] > 0");
                });

            migrationBuilder.CreateTable(
                name: "SEASON",
                schema: "dbo",
                columns: table => new
                {
                    season_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    year = table.Column<short>(type: "smallint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SEASON", x => x.season_id);
                    table.UniqueConstraint("UQ_SEASON_year", x => x.year);
                    table.CheckConstraint("CK_SEASON_dates", "[end_date] IS NULL OR [start_date] IS NULL OR [end_date] >= [start_date]");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DIVISION",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SEASON",
                schema: "dbo");
        }
    }
}
