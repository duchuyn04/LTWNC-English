using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class PreserveQuizHistoryAndPendingSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuizSessionQuestions_Flashcards_FlashcardId",
                table: "QuizSessionQuestions");

            migrationBuilder.DropIndex(
                name: "IX_QuizSessionQuestions_FlashcardId",
                table: "QuizSessionQuestions");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CompletedAt",
                table: "StudySessions",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "This migration is forward-only because deleted flashcards may be referenced by preserved Quiz history.");
        }
    }
}
