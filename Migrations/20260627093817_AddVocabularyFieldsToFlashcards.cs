using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabularyFieldsToFlashcards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExampleMeaning",
                table: "Flashcards",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExampleSentence",
                table: "Flashcards",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsStarred",
                table: "Flashcards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PartOfSpeech",
                table: "Flashcards",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Pronunciation",
                table: "Flashcards",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Synonyms",
                table: "Flashcards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Flashcards_FlashcardSetId_IsStarred",
                table: "Flashcards",
                columns: new[] { "FlashcardSetId", "IsStarred" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Flashcards_FlashcardSetId_IsStarred",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "ExampleMeaning",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "ExampleSentence",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "IsStarred",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "PartOfSpeech",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "Pronunciation",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "Synonyms",
                table: "Flashcards");
        }
    }
}
