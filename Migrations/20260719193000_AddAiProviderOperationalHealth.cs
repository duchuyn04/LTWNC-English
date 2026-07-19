using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    public partial class AddAiProviderOperationalHealth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bỏ index cũ vì index mới có cùng prefix và thêm cột Succeeded cho truy vấn tỷ lệ lỗi.
            migrationBuilder.DropIndex(
                name: "IX_AiOperationLogs_ProviderId_OccurredAtUtc",
                table: "AiOperationLogs");

            // Thêm metadata fallback để mỗi lần gọi AI ghi được thứ tự thử provider.
            migrationBuilder.AddColumn<int>(
                name: "FallbackAttempt",
                table: "AiOperationLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Thêm bộ đếm lỗi health check liên tiếp để Admin thấy provider không ổn định.
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailureCount",
                table: "AiProviders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Tạo index cho truy vấn tỷ lệ lỗi theo provider trong cửa sổ thời gian ngắn.
            migrationBuilder.CreateIndex(
                name: "IX_AiOperationLogs_ProviderId_OccurredAtUtc_Succeeded",
                table: "AiOperationLogs",
                columns: new[] { "ProviderId", "OccurredAtUtc", "Succeeded" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback lần lượt index và hai cột đã thêm ở Up.
            migrationBuilder.DropIndex(
                name: "IX_AiOperationLogs_ProviderId_OccurredAtUtc_Succeeded",
                table: "AiOperationLogs");

            migrationBuilder.DropColumn(
                name: "FallbackAttempt",
                table: "AiOperationLogs");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailureCount",
                table: "AiProviders");

            migrationBuilder.CreateIndex(
                name: "IX_AiOperationLogs_ProviderId_OccurredAtUtc",
                table: "AiOperationLogs",
                columns: new[] { "ProviderId", "OccurredAtUtc" });
        }
    }
}
