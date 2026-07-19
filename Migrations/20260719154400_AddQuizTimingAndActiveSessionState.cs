using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizTimingAndActiveSessionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudySessions_UserId_FlashcardSetId_Mode",
                table: "StudySessions");

            migrationBuilder.AddColumn<DateTime>(
                name: "QuizStartedAtUtc",
                table: "StudySessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuizTimeLimitSeconds",
                table: "StudySessions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_UserId_FlashcardSetId_Mode",
                table: "StudySessions",
                columns: new[] { "UserId", "FlashcardSetId", "Mode" },
                unique: true,
                filter: "[Mode] = 1 AND [Score] IS NULL AND [CompletedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudySessions_UserId_FlashcardSetId_Mode",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "QuizStartedAtUtc",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "QuizTimeLimitSeconds",
                table: "StudySessions");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_UserId_FlashcardSetId_Mode",
                table: "StudySessions",
                columns: new[] { "UserId", "FlashcardSetId", "Mode" },
                unique: true,
                filter: "[Mode] = 1 AND [Score] IS NULL");
        }
    }
}
