using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
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
