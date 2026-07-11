using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceSetIdToFlashcardSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceSetId",
                table: "FlashcardSets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardSets_UserId_SourceSetId",
                table: "FlashcardSets",
                columns: new[] { "UserId", "SourceSetId" },
                unique: true,
                filter: "[SourceSetId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FlashcardSets_UserId_SourceSetId",
                table: "FlashcardSets");

            migrationBuilder.DropColumn(
                name: "SourceSetId",
                table: "FlashcardSets");
        }
    }
}
