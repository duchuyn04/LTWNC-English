using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ltwnc.Data;

#nullable disable

namespace ltwnc.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260731045208_AddSpacedReview")]
public partial class AddSpacedReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReviewProgresses",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                FlashcardId = table.Column<int>(type: "int", nullable: false),
                Stage = table.Column<int>(type: "int", nullable: false),
                NextReviewAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LongTermIntervalDays = table.Column<int>(type: "int", nullable: false),
                LastRatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReviewProgresses", value => value.Id);
                table.ForeignKey(
                    name: "FK_ReviewProgresses_Flashcards_FlashcardId",
                    column: value => value.FlashcardId,
                    principalTable: "Flashcards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ReviewSessions",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ReviewSessions", value => value.Id));

        migrationBuilder.CreateTable(
            name: "ReviewSessionItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ReviewSessionId = table.Column<int>(type: "int", nullable: false),
                FlashcardId = table.Column<int>(type: "int", nullable: false),
                OrderIndex = table.Column<int>(type: "int", nullable: false),
                IsNewCardAtAssignment = table.Column<bool>(type: "bit", nullable: false),
                Rating = table.Column<int>(type: "int", nullable: true),
                RatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                PreviousStage = table.Column<int>(type: "int", nullable: false),
                NextStage = table.Column<int>(type: "int", nullable: false),
                PreviousNextReviewAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                NextReviewAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                PreviousLongTermIntervalDays = table.Column<int>(type: "int", nullable: false),
                NextLongTermIntervalDays = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReviewSessionItems", value => value.Id);
                table.ForeignKey(
                    name: "FK_ReviewSessionItems_Flashcards_FlashcardId",
                    column: value => value.FlashcardId,
                    principalTable: "Flashcards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReviewSessionItems_ReviewSessions_ReviewSessionId",
                    column: value => value.ReviewSessionId,
                    principalTable: "ReviewSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ReviewProgresses_FlashcardId",
            table: "ReviewProgresses",
            column: "FlashcardId");
        migrationBuilder.CreateIndex(
            name: "IX_ReviewProgresses_UserId_FlashcardId",
            table: "ReviewProgresses",
            columns: new[] { "UserId", "FlashcardId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ReviewProgresses_UserId_NextReviewAtUtc",
            table: "ReviewProgresses",
            columns: new[] { "UserId", "NextReviewAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_ReviewSessionItems_FlashcardId",
            table: "ReviewSessionItems",
            column: "FlashcardId");
        migrationBuilder.CreateIndex(
            name: "IX_ReviewSessionItems_ReviewSessionId_FlashcardId",
            table: "ReviewSessionItems",
            columns: new[] { "ReviewSessionId", "FlashcardId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ReviewSessionItems_ReviewSessionId_OrderIndex",
            table: "ReviewSessionItems",
            columns: new[] { "ReviewSessionId", "OrderIndex" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ReviewSessions_UserId_CompletedAtUtc",
            table: "ReviewSessions",
            columns: new[] { "UserId", "CompletedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_ReviewSessions_UserId_Active",
            table: "ReviewSessions",
            column: "UserId",
            unique: true,
            filter: "[CompletedAtUtc] IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReviewProgresses");
        migrationBuilder.DropTable(name: "ReviewSessionItems");
        migrationBuilder.DropTable(name: "ReviewSessions");
    }
}
