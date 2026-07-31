using ltwnc.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260731070000_AddReviewSessionEndState")]
public partial class AddReviewSessionEndState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EndedAtUtc",
            table: "ReviewSessions",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.DropIndex(
            name: "IX_ReviewSessions_UserId_Active",
            table: "ReviewSessions");

        migrationBuilder.CreateIndex(
            name: "IX_ReviewSessions_UserId_Active",
            table: "ReviewSessions",
            column: "UserId",
            unique: true,
            filter: "[CompletedAtUtc] IS NULL AND [EndedAtUtc] IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ReviewSessions_UserId_Active",
            table: "ReviewSessions");

        migrationBuilder.CreateIndex(
            name: "IX_ReviewSessions_UserId_Active",
            table: "ReviewSessions",
            column: "UserId",
            unique: true,
            filter: "[CompletedAtUtc] IS NULL");

        migrationBuilder.DropColumn(
            name: "EndedAtUtc",
            table: "ReviewSessions");
    }
}
