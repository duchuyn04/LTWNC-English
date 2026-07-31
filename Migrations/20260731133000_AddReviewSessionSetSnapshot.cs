using ltwnc.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260731133000_AddReviewSessionSetSnapshot")]
public partial class AddReviewSessionSetSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FlashcardSetId",
            table: "ReviewSessions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SettingsSnapshotJson",
            table: "ReviewSessions",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReviewSessions_UserId_FlashcardSetId_CompletedAtUtc",
            table: "ReviewSessions",
            columns: new[] { "UserId", "FlashcardSetId", "CompletedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ReviewSessions_UserId_FlashcardSetId_CompletedAtUtc",
            table: "ReviewSessions");

        migrationBuilder.DropColumn(
            name: "FlashcardSetId",
            table: "ReviewSessions");

        migrationBuilder.DropColumn(
            name: "SettingsSnapshotJson",
            table: "ReviewSessions");
    }
}
