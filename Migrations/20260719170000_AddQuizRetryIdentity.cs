using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizRetryIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuizRetryKind",
                table: "StudySessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuizRetrySourceSessionId",
                table: "StudySessions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuizRetryKind",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "QuizRetrySourceSessionId",
                table: "StudySessions");
        }
    }
}
