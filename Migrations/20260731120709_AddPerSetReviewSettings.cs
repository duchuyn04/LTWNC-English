using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddPerSetReviewSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FlashcardSetId = table.Column<int>(type: "int", nullable: false),
                    ReviewSessionSize = table.Column<int>(type: "int", nullable: false, defaultValue: 20),
                    NewCardQuota = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    ReviewMaxIntervalDays = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
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
                    table.PrimaryKey("PK_ReviewSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSettings_FlashcardSets_FlashcardSetId",
                        column: x => x.FlashcardSetId,
                        principalTable: "FlashcardSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill một cấu hình cho mỗi bộ thẻ đã sở hữu. Giá trị cũ ngoài
            // miền hợp lệ được đưa về mặc định để migration không làm hỏng dữ liệu.
            migrationBuilder.Sql(@"
INSERT INTO [ReviewSettings]
    ([UserId], [FlashcardSetId], [ReviewSessionSize], [NewCardQuota], [ReviewMaxIntervalDays],
     [ShowFrontTerm], [ShowFrontDefinition], [ShowFrontIpa], [ShowFrontImage],
     [ShowBackTerm], [ShowBackDefinition], [ShowBackIpa], [ShowBackExample], [ShowBackImage],
     [HideImage], [BlurImage], [LargeImage], [PronounceFront], [PronounceBack])
SELECT
    fs.[UserId],
    fs.[Id],
    CASE WHEN us.[ReviewSessionSize] BETWEEN 5 AND 100 THEN us.[ReviewSessionSize] ELSE 20 END,
    CASE WHEN fs.[NewCardQuota] BETWEEN 0 AND 20 THEN fs.[NewCardQuota] ELSE 5 END,
    CASE WHEN us.[ReviewMaxIntervalDays] BETWEEN 30 AND 365 THEN us.[ReviewMaxIntervalDays] ELSE 30 END,
    ISNULL(us.[ShowFrontTerm], 1),
    ISNULL(us.[ShowFrontDefinition], 0),
    ISNULL(us.[ShowFrontIpa], 1),
    ISNULL(us.[ShowFrontImage], 0),
    ISNULL(us.[ShowBackTerm], 0),
    ISNULL(us.[ShowBackDefinition], 1),
    ISNULL(us.[ShowBackIpa], 0),
    ISNULL(us.[ShowBackExample], 1),
    ISNULL(us.[ShowBackImage], 1),
    ISNULL(us.[HideImage], 0),
    ISNULL(us.[BlurImage], 0),
    ISNULL(us.[LargeImage], 0),
    ISNULL(us.[PronounceFront], 1),
    ISNULL(us.[PronounceBack], 0)
FROM [FlashcardSets] AS fs
LEFT JOIN [UserStudySettings] AS us ON us.[UserId] = fs.[UserId]
WHERE NOT EXISTS
(
    SELECT 1
    FROM [ReviewSettings] AS existing
    WHERE existing.[UserId] = fs.[UserId]
      AND existing.[FlashcardSetId] = fs.[Id]
);");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSettings_FlashcardSetId",
                table: "ReviewSettings",
                column: "FlashcardSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSettings_UserId_FlashcardSetId",
                table: "ReviewSettings",
                columns: new[] { "UserId", "FlashcardSetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewSettings");
        }
    }
}
