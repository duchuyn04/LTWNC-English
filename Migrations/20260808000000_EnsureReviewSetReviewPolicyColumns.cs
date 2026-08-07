using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ltwnc.Data;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Bổ sung các cột/chỉ mục của AddReviewSetReviewPolicy cho các database
    /// có __EFMigrationsHistory bị đánh dấu sẵn (vd. database tạo từ database.sql
    /// cũ) nên EF không bao giờ tự chạy lại migration gốc. SQL idempotent:
    /// chỉ thêm khi cột/chỉ mục chưa tồn tại, an toàn chạy trên mọi database.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260808000000_EnsureReviewSetReviewPolicyColumns")]
    public partial class EnsureReviewSetReviewPolicyColumns : Migration
    {
        // Chạy một lần trên database production bị lệch migration history.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('ReviewSessionItems', 'NewCardAssignedDate') IS NULL
                BEGIN
                    ALTER TABLE [ReviewSessionItems] ADD [NewCardAssignedDate] datetime2 NULL;
                END;

                IF COL_LENGTH('FlashcardSets', 'NewCardQuota') IS NULL
                BEGIN
                    ALTER TABLE [FlashcardSets] ADD [NewCardQuota] int NOT NULL DEFAULT 5;
                END;

                IF COL_LENGTH('FlashcardSets', 'ReviewPaused') IS NULL
                BEGIN
                    ALTER TABLE [FlashcardSets] ADD [ReviewPaused] bit NOT NULL DEFAULT CAST(0 AS bit);
                END;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_ReviewSessionItems_NewCardAssignedDate'
                      AND object_id = OBJECT_ID('ReviewSessionItems'))
                BEGIN
                    CREATE INDEX [IX_ReviewSessionItems_NewCardAssignedDate]
                        ON [ReviewSessionItems] ([NewCardAssignedDate]);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_ReviewSessionItems_NewCardAssignedDate'
                      AND object_id = OBJECT_ID('ReviewSessionItems'))
                BEGIN
                    DROP INDEX [IX_ReviewSessionItems_NewCardAssignedDate]
                        ON [ReviewSessionItems];
                END;
                """);
        }
    }
}
