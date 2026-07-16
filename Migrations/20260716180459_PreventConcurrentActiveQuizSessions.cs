using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class PreventConcurrentActiveQuizSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_UserId_FlashcardSetId_Mode",
                table: "StudySessions",
                columns: new[] { "UserId", "FlashcardSetId", "Mode" },
                unique: true,
                filter: "[Mode] = 1 AND [Score] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudySessions_UserId_FlashcardSetId_Mode",
                table: "StudySessions");
        }
    }
}
