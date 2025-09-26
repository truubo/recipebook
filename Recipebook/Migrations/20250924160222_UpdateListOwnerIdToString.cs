using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recipebook.Migrations
{
    /// <inheritdoc />
    public partial class UpdateListOwnerIdToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ListRecipes_ListId",
                table: "ListRecipes");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                table: "Lists",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_ListRecipes_ListId_RecipeId",
                table: "ListRecipes",
                columns: new[] { "ListId", "RecipeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ListRecipes_ListId_RecipeId",
                table: "ListRecipes");

            migrationBuilder.AlterColumn<int>(
                name: "OwnerId",
                table: "Lists",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_ListRecipes_ListId",
                table: "ListRecipes",
                column: "ListId");
        }
    }
}
