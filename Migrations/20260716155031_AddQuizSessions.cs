using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuizSessionQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudySessionId = table.Column<int>(type: "int", nullable: false),
                    FlashcardId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    PromptText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Choice1Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Choice2Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Choice3Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Choice4Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrectChoiceIndex = table.Column<int>(type: "int", nullable: false),
                    SelectedChoiceIndex = table.Column<int>(type: "int", nullable: true),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizSessionQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizSessionQuestions_Flashcards_FlashcardId",
                        column: x => x.FlashcardId,
                        principalTable: "Flashcards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuizSessionQuestions_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessionQuestions_FlashcardId",
                table: "QuizSessionQuestions",
                column: "FlashcardId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessionQuestions_StudySessionId",
                table: "QuizSessionQuestions",
                column: "StudySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessionQuestions_StudySessionId_FlashcardId",
                table: "QuizSessionQuestions",
                columns: new[] { "StudySessionId", "FlashcardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessionQuestions_StudySessionId_OrderIndex",
                table: "QuizSessionQuestions",
                columns: new[] { "StudySessionId", "OrderIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizSessionQuestions");
        }
    }
}
