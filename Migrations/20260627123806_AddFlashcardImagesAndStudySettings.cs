using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddFlashcardImagesAndStudySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Flashcards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadedImagePath",
                table: "Flashcards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserStudySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StarredOnly = table.Column<bool>(type: "bit", nullable: false),
                    UnlearnedOnly = table.Column<bool>(type: "bit", nullable: false),
                    ShowFrontTerm = table.Column<bool>(type: "bit", nullable: false),
                    ShowFrontDefinition = table.Column<bool>(type: "bit", nullable: false),
                    ShowFrontIpa = table.Column<bool>(type: "bit", nullable: false),
                    ShowFrontImage = table.Column<bool>(type: "bit", nullable: false),
                    ShowBackTerm = table.Column<bool>(type: "bit", nullable: false),
                    ShowBackDefinition = table.Column<bool>(type: "bit", nullable: false),
                    ShowBackIpa = table.Column<bool>(type: "bit", nullable: false),
                    ShowBackExample = table.Column<bool>(type: "bit", nullable: false),
                    ShowBackImage = table.Column<bool>(type: "bit", nullable: false),
                    HideImage = table.Column<bool>(type: "bit", nullable: false),
                    BlurImage = table.Column<bool>(type: "bit", nullable: false),
                    LargeImage = table.Column<bool>(type: "bit", nullable: false),
                    PronounceFront = table.Column<bool>(type: "bit", nullable: false),
                    PronounceBack = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStudySettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserStudySettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserStudySettings_UserId",
                table: "UserStudySettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserStudySettings");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "UploadedImagePath",
                table: "Flashcards");
        }
    }
}
