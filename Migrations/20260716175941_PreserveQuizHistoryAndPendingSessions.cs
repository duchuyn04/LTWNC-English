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
            migrationBuilder.AlterColumn<DateTime>(
                name: "CompletedAt",
                table: "StudySessions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessionQuestions_FlashcardId",
                table: "QuizSessionQuestions",
                column: "FlashcardId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuizSessionQuestions_Flashcards_FlashcardId",
                table: "QuizSessionQuestions",
                column: "FlashcardId",
                principalTable: "Flashcards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
