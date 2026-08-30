using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LigaVolley.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClubLogoMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "logo_content_type",
                schema: "dbo",
                table: "CLUB",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_storage_key",
                schema: "dbo",
                table: "CLUB",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "logo_version",
                schema: "dbo",
                table: "CLUB",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "logo_content_type",
                schema: "dbo",
                table: "CLUB");

            migrationBuilder.DropColumn(
                name: "logo_storage_key",
                schema: "dbo",
                table: "CLUB");

            migrationBuilder.DropColumn(
                name: "logo_version",
                schema: "dbo",
                table: "CLUB");
        }
    }
}
