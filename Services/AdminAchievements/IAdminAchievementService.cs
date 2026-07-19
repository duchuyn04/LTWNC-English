namespace ltwnc.Services.AdminAchievements;

// Cổng quản trị thành tích: chỉ đọc danh mục/kết quả và đồng bộ lại từ cơ chế hiện có.
public interface IAdminAchievementService
{
    // Lấy trang tổng quan thành tích, gồm danh mục từ mã nguồn và kết quả theo từng người dùng.
    Task<AdminAchievementOverview> GetOverviewAsync(
        AdminAchievementQuery query,
        CancellationToken cancellationToken = default);

    // Đồng bộ lại cho một người dùng, không cấp hoặc thu hồi thủ công mã thành tích nào.
    Task<AdminAchievementSyncResult> ResyncUserAsync(
        AdminAchievementSyncCommand command,
        CancellationToken cancellationToken = default);

    // Đồng bộ lại toàn hệ thống theo lô nhỏ, dùng chung quy tắc tính thành tích hiện có.
    Task<AdminAchievementBatchSyncResult> ResyncAllAsync(
        AdminAchievementBatchSyncCommand command,
        CancellationToken cancellationToken = default);
}
