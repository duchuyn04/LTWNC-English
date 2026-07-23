using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class FixDictationSessionIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ;WITH DuplicateAnswers AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY StudySessionId, FlashcardId
                            ORDER BY Id
                        ) AS DuplicateNumber
                    FROM DictationSessionDetails
                )
                DELETE FROM DictationSessionDetails
                WHERE Id IN
                (
                    SELECT Id
                    FROM DuplicateAnswers
                    WHERE DuplicateNumber > 1
                );
                """);

            migrationBuilder.CreateTable(
                name: "DictationSessionQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudySessionId = table.Column<int>(type: "int", nullable: false),
                    FlashcardId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    PromptText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrectAnswer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Term = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Definition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pronunciation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExampleSentence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExampleMeaning = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Synonyms = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnsweredText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DictationSessionQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DictationSessionQuestions_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO DictationSessionQuestions
                (
                    StudySessionId,
                    FlashcardId,
                    OrderIndex,
                    PromptText,
                    CorrectAnswer,
                    Term,
                    Definition,
                    Pronunciation,
                    ExampleSentence,
                    ExampleMeaning,
                    Synonyms,
                    AnsweredText,
                    IsCorrect,
                    AnsweredAt
                )
                SELECT
                    detail.StudySessionId,
                    detail.FlashcardId,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY detail.StudySessionId
                        ORDER BY detail.Id
                    ) - 1,
                    CASE
                        WHEN sessionRow.DictationContentMode = 1
                            THEN flashcard.ExampleSentence
                        ELSE flashcard.FrontText
                    END,
                    CASE
                        WHEN sessionRow.DictationContentMode = 1
                            THEN flashcard.ExampleSentence
                        ELSE flashcard.FrontText
                    END,
                    flashcard.FrontText,
                    flashcard.BackText,
                    flashcard.Pronunciation,
                    flashcard.ExampleSentence,
                    flashcard.ExampleMeaning,
                    flashcard.Synonyms,
                    detail.AnsweredText,
                    detail.IsCorrect,
                    detail.CreatedAt
                FROM DictationSessionDetails AS detail
                INNER JOIN StudySessions AS sessionRow
                    ON sessionRow.Id = detail.StudySessionId
                INNER JOIN Flashcards AS flashcard
                    ON flashcard.Id = detail.FlashcardId
                WHERE sessionRow.Mode = 4;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DictationSessionDetails_StudySessionId_FlashcardId",
                table: "DictationSessionDetails",
                columns: new[] { "StudySessionId", "FlashcardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DictationSessionQuestions_StudySessionId_FlashcardId",
                table: "DictationSessionQuestions",
                columns: new[] { "StudySessionId", "FlashcardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DictationSessionQuestions_StudySessionId_OrderIndex",
                table: "DictationSessionQuestions",
                columns: new[] { "StudySessionId", "OrderIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DictationSessionQuestions");

            migrationBuilder.DropIndex(
                name: "IX_DictationSessionDetails_StudySessionId_FlashcardId",
                table: "DictationSessionDetails");
        }
    }
}
