using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ltwnc.Data;

#nullable disable

namespace ltwnc.Migrations
{
    // Gắn metadata để EF CLI nhận diện migration thủ công khi chạy database update.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260719194500_AddAdminGlobalSearchIndexes")]
    public partial class AddAdminGlobalSearchIndexes : Migration
    {
        // Tạo index metadata phục vụ tìm kiếm toàn cục an toàn cho Admin.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thêm index metadata cho tìm kiếm Admin theo prefix tiêu đề bộ, không index nội dung thẻ.
            migrationBuilder.CreateIndex(
                name: "IX_FlashcardSets_Title",
                table: "FlashcardSets",
                column: "Title");
        }

        // Gỡ index metadata nếu rollback migration.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Gỡ index tìm kiếm Admin khi rollback migration.
            migrationBuilder.DropIndex(
                name: "IX_FlashcardSets_Title",
                table: "FlashcardSets");
        }
    }
}
