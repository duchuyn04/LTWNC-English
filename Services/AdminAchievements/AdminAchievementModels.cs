namespace ltwnc.Services.AdminAchievements;

// Điều kiện tìm kiếm trang quản trị Thành tích.
public sealed record AdminAchievementQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = AdminAchievementService.DefaultPageSize);

// Một dòng danh mục thành tích đọc từ mã nguồn, kèm số người đã nhận trong database.
public sealed record AdminAchievementDefinitionSummary(
    string Code,
    string Title,
    string Description,
    string Metric,
    int Target,
    int RecipientCount);

// Kết quả thành tích theo một người dùng trong bảng quản trị.
public sealed record AdminAchievementUserResult(
    string UserId,
    string UserName,
    string Email,
    int UnlockedCount,
    int EligibleCount,
    int MissingCount,
    DateTime? LastUnlockedAtUtc,
    IReadOnlyList<string> MissingCodes);

// Dữ liệu tổng hợp cho màn hình quản trị Thành tích.
public sealed record AdminAchievementOverview(
    IReadOnlyList<AdminAchievementDefinitionSummary> Catalog,
    IReadOnlyList<AdminAchievementUserResult> UserResults,
    int TotalUsers,
    int Page,
    int PageSize);

// Lệnh đồng bộ lại thành tích cho một người dùng.
public sealed record AdminAchievementSyncCommand(
    string ActorUserId,
    string ActorDisplay,
    string TargetUserId,
    string? Reason,
    bool Confirmed,
    string? CorrelationId = null);

// Lệnh đồng bộ lại thành tích toàn hệ thống theo lô.
public sealed record AdminAchievementBatchSyncCommand(
    string ActorUserId,
    string ActorDisplay,
    string? Reason,
    bool Confirmed,
    int BatchSize,
    string? CorrelationId = null);

// Kết quả đồng bộ cho một người dùng.
public sealed record AdminAchievementSyncResult(
    bool Succeeded,
    string Message,
    int ChangedCount,
    int FailedCount)
{
    // Tạo kết quả thành công với thông báo tiếng Việt.
    public static AdminAchievementSyncResult Success(string message, int changedCount)
    {
        return new AdminAchievementSyncResult(true, message, changedCount, 0);
    }

    // Tạo kết quả thất bại với thông báo tiếng Việt.
    public static AdminAchievementSyncResult Failure(string message, int failedCount = 1)
    {
        return new AdminAchievementSyncResult(false, message, 0, failedCount);
    }
}

// Kết quả đồng bộ toàn hệ thống, có số user đã xử lý và số thành tích mới.
public sealed record AdminAchievementBatchSyncResult(
    bool Succeeded,
    string Message,
    int ProcessedUsers,
    int ChangedCount,
    int FailedCount)
{
    // Tạo kết quả đồng bộ toàn hệ thống sau khi đã xử lý hết user trong batch.
    public static AdminAchievementBatchSyncResult FromCounts(
        int processedUsers,
        int changedCount,
        int failedCount)
    {
        if (failedCount > 0)
        {
            return new AdminAchievementBatchSyncResult(
                false,
                $"Đã đồng bộ {processedUsers:N0} người dùng, thêm {changedCount:N0} thành tích và có {failedCount:N0} lỗi cần kiểm tra.",
                processedUsers,
                changedCount,
                failedCount);
        }

        return new AdminAchievementBatchSyncResult(
            true,
            $"Đã đồng bộ {processedUsers:N0} người dùng và thêm {changedCount:N0} thành tích còn thiếu.",
            processedUsers,
            changedCount,
            0);
    }

    // Tạo kết quả thất bại trước khi bắt đầu đồng bộ.
    public static AdminAchievementBatchSyncResult Failure(string message)
    {
        return new AdminAchievementBatchSyncResult(false, message, 0, 0, 1);
    }
}
