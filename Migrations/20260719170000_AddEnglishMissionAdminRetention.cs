using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ltwnc.Data;

#nullable disable

namespace ltwnc.Migrations
{
    // Gắn metadata để EF CLI nhận diện migration thủ công khi chạy database update.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260719170000_AddEnglishMissionAdminRetention")]
    public partial class AddEnglishMissionAdminRetention : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConversationContentDeletedAtUtc",
                table: "EnglishMissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConversationRetentionCaseReference",
                table: "EnglishMissions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConversationRetentionCaseType",
                table: "EnglishMissions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConversationRetentionHoldUntilUtc",
                table: "EnglishMissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnglishMissions_ConversationContentDeletedAtUtc_CreatedAt",
                table: "EnglishMissions",
                columns: new[] { "ConversationContentDeletedAtUtc", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EnglishMissions_ConversationContentDeletedAtUtc_CreatedAt",
                table: "EnglishMissions");

            migrationBuilder.DropColumn(
                name: "ConversationContentDeletedAtUtc",
                table: "EnglishMissions");

            migrationBuilder.DropColumn(
                name: "ConversationRetentionCaseReference",
                table: "EnglishMissions");

            migrationBuilder.DropColumn(
                name: "ConversationRetentionCaseType",
                table: "EnglishMissions");

            migrationBuilder.DropColumn(
                name: "ConversationRetentionHoldUntilUtc",
                table: "EnglishMissions");
        }
    }
}
