using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddDictationSessionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DictationAcceptSynonyms",
                table: "UserStudySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DictationAnswerMode",
                table: "UserStudySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "DictationAutoAdvance",
                table: "UserStudySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<float>(
                name: "DictationPlaybackSpeed",
                table: "UserStudySettings",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<bool>(
                name: "DictationShowHint",
                table: "UserStudySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DictationShuffle",
                table: "UserStudySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DictationVoiceUri",
                table: "UserStudySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DictationSessionDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudySessionId = table.Column<int>(type: "int", nullable: false),
                    FlashcardId = table.Column<int>(type: "int", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    AnsweredText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DictationSessionDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DictationSessionDetails_Flashcards_FlashcardId",
                        column: x => x.FlashcardId,
                        principalTable: "Flashcards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DictationSessionDetails_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DictationSessionDetails_FlashcardId",
                table: "DictationSessionDetails",
                column: "FlashcardId");

            migrationBuilder.CreateIndex(
                name: "IX_DictationSessionDetails_StudySessionId",
                table: "DictationSessionDetails",
                column: "StudySessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DictationSessionDetails");

            migrationBuilder.DropColumn(
                name: "DictationAcceptSynonyms",
                table: "UserStudySettings");

            migrationBuilder.DropColumn(
                name: "DictationAnswerMode",
                table: "UserStudySettings");

            migrationBuilder.DropColumn(
                name: "DictationAutoAdvance",
                table: "UserStudySettings");

            migrationBuilder.DropColumn(
                name: "DictationPlaybackSpeed",
                table: "UserStudySettings");

            migrationBuilder.DropColumn(
                name: "DictationShowHint",
                table: "UserStudySettings");

            migrationBuilder.DropColumn(
                name: "DictationShuffle",
                table: "UserStudySettings");

            migrationBuilder.DropColumn(
                name: "DictationVoiceUri",
                table: "UserStudySettings");
        }
    }
}
