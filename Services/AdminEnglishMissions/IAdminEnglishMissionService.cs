namespace ltwnc.Services.AdminEnglishMissions;

public interface IAdminEnglishMissionService
{
    // Tìm danh sách mission ở mức summary, lọc/sắp xếp/phân trang phía server.
    Task<AdminEnglishMissionPage> SearchAsync(
        AdminEnglishMissionQuery query,
        CancellationToken cancellationToken = default);

    // Mở hội thoại chi tiết sau khi qua cổng vụ việc và ghi audit thành công.
    Task<AdminEnglishMissionConversationResult> GetConversationAsync(
        AdminEnglishMissionAccessCommand command,
        CancellationToken cancellationToken = default);

    // Dọn nội dung hội thoại hết hạn theo batch nhỏ và có thể chạy lặp an toàn.
    Task<AdminEnglishMissionCleanupResult> CleanupExpiredConversationContentAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}
