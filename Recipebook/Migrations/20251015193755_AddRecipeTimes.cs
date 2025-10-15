using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recipebook.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CookTimeMinutes",
                table: "Recipe",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrepTimeMinutes",
                table: "Recipe",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CookTimeMinutes",
                table: "Recipe");

            migrationBuilder.DropColumn(
                name: "PrepTimeMinutes",
                table: "Recipe");
        }
    }
}
