using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewSetReviewPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NewCardAssignedDate",
                table: "ReviewSessionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewCardQuota",
                table: "FlashcardSets",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewPaused",
                table: "FlashcardSets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSessionItems_NewCardAssignedDate",
                table: "ReviewSessionItems",
                column: "NewCardAssignedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReviewSessionItems_NewCardAssignedDate",
                table: "ReviewSessionItems");

            migrationBuilder.DropColumn(
                name: "NewCardAssignedDate",
                table: "ReviewSessionItems");

            migrationBuilder.DropColumn(
                name: "NewCardQuota",
                table: "FlashcardSets");

            migrationBuilder.DropColumn(
                name: "ReviewPaused",
                table: "FlashcardSets");
        }
    }
}
