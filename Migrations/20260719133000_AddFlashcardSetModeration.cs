using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ltwnc.Data;

#nullable disable

namespace ltwnc.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260719133000_AddFlashcardSetModeration")]
    public partial class AddFlashcardSetModeration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModerationStatus",
                table: "FlashcardSets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "ModerationPublicReason",
                table: "FlashcardSets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationInternalNote",
                table: "FlashcardSets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationEvidence",
                table: "FlashcardSets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeratedByUserId",
                table: "FlashcardSets",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModeratedAtUtc",
                table: "FlashcardSets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerationVersion",
                table: "FlashcardSets",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardSets_IsPublic_ModerationStatus_UpdatedAt",
                table: "FlashcardSets",
                columns: new[] { "IsPublic", "ModerationStatus", "UpdatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FlashcardSets_IsPublic_ModerationStatus_UpdatedAt",
                table: "FlashcardSets");

            migrationBuilder.DropColumn(
                name: "ModeratedAtUtc",
                table: "FlashcardSets");

            migrationBuilder.DropColumn(
                name: "ModeratedByUserId",
                table: "FlashcardSets");

            migrationBuilder.DropColumn(
                name: "ModerationEvidence",
                table: "FlashcardSets");

            migrationBuilder.DropColumn(
                name: "ModerationInternalNote",
                table: "FlashcardSets");

            migrationBuilder.DropColumn(
                name: "ModerationPublicReason",
                table: "FlashcardSets");

            migrationBuilder.DropColumn(
                name: "ModerationStatus",
                table: "FlashcardSets");

            migrationBuilder.DropColumn(
                name: "ModerationVersion",
                table: "FlashcardSets");
        }
    }
}

