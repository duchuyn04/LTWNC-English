using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddEnglishMissionSuggestedReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuggestedReplyEn",
                table: "EnglishMissions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedReplyVi",
                table: "EnglishMissions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuggestedReplyEn",
                table: "EnglishMissions");

            migrationBuilder.DropColumn(
                name: "SuggestedReplyVi",
                table: "EnglishMissions");
        }
    }
}
