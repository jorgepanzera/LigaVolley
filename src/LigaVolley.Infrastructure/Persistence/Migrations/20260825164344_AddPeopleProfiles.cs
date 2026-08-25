using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPeopleProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PERSON",
                schema: "dbo",
                columns: table => new
                {
                    person_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    document_type = table.Column<string>(type: "varchar(20)", nullable: true),
                    document_number = table.Column<string>(type: "varchar(30)", nullable: true),
                    first_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "char(1)", nullable: true),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PERSON", x => x.person_id);
                    table.CheckConstraint("CK_PERSON_document", "([document_type] IS NULL AND [document_number] IS NULL) OR ([document_type] IS NOT NULL AND [document_number] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "COACH",
                schema: "dbo",
                columns: table => new
                {
                    coach_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COACH", x => x.coach_id);
                    table.ForeignKey(
                        name: "FK_COACH_PERSON",
                        column: x => x.person_id,
                        principalSchema: "dbo",
                        principalTable: "PERSON",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PERSON_ADDITIONAL_DOCUMENT",
                schema: "dbo",
                columns: table => new
                {
                    person_additional_document_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    document_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    document_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PERSON_ADDITIONAL_DOCUMENT", x => x.person_additional_document_id);
                    table.CheckConstraint("CK_PERSON_ADDITIONAL_DOCUMENT_dates", "[valid_to] IS NULL OR [valid_from] IS NULL OR [valid_to] >= [valid_from]");
                    table.ForeignKey(
                        name: "FK_PERSON_ADDITIONAL_DOCUMENT_PERSON",
                        column: x => x.person_id,
                        principalSchema: "dbo",
                        principalTable: "PERSON",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PLAYER",
                schema: "dbo",
                columns: table => new
                {
                    player_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PLAYER", x => x.player_id);
                    table.ForeignKey(
                        name: "FK_PLAYER_PERSON",
                        column: x => x.person_id,
                        principalSchema: "dbo",
                        principalTable: "PERSON",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REFEREE",
                schema: "dbo",
                columns: table => new
                {
                    referee_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REFEREE", x => x.referee_id);
                    table.ForeignKey(
                        name: "FK_REFEREE_PERSON",
                        column: x => x.person_id,
                        principalSchema: "dbo",
                        principalTable: "PERSON",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_COACH_person",
                schema: "dbo",
                table: "COACH",
                column: "person_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PERSON_name",
                schema: "dbo",
                table: "PERSON",
                columns: new[] { "last_name", "first_name", "person_id" });

            migrationBuilder.CreateIndex(
                name: "UX_PERSON_document",
                schema: "dbo",
                table: "PERSON",
                columns: new[] { "document_type", "document_number" },
                unique: true,
                filter: "[document_type] IS NOT NULL AND [document_number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PERSON_ADDITIONAL_DOCUMENT_lookup",
                schema: "dbo",
                table: "PERSON_ADDITIONAL_DOCUMENT",
                columns: new[] { "person_id", "document_type", "active" });

            migrationBuilder.CreateIndex(
                name: "UQ_PLAYER_person",
                schema: "dbo",
                table: "PLAYER",
                column: "person_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_REFEREE_person",
                schema: "dbo",
                table: "REFEREE",
                column: "person_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COACH",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PERSON_ADDITIONAL_DOCUMENT",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PLAYER",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "REFEREE",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PERSON",
                schema: "dbo");
        }
    }
}
