using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
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
