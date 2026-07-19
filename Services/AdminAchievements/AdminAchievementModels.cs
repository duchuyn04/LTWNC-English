namespace ltwnc.Services.AdminAchievements;

// Dieu kien tim kiem trang Admin/Thanh tich.
public sealed record AdminAchievementQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = AdminAchievementService.DefaultPageSize);

// Mot dong catalog thanh tich doc tu source code, kem so nguoi da nhan trong database.
public sealed record AdminAchievementDefinitionSummary(
    string Code,
    string Title,
    string Description,
    string Metric,
    int Target,
    int RecipientCount);

// Ket qua thanh tich theo mot nguoi dung trong bang Admin.
public sealed record AdminAchievementUserResult(
    string UserId,
    string UserName,
    string Email,
    int UnlockedCount,
    int EligibleCount,
    int MissingCount,
    DateTime? LastUnlockedAtUtc,
    IReadOnlyList<string> MissingCodes);

// Du lieu tong hop cho man hinh Admin thanh tich.
public sealed record AdminAchievementOverview(
    IReadOnlyList<AdminAchievementDefinitionSummary> Catalog,
    IReadOnlyList<AdminAchievementUserResult> UserResults,
    int TotalUsers,
    int Page,
    int PageSize);

// Lenh dong bo lai thanh tich cho mot nguoi dung.
public sealed record AdminAchievementSyncCommand(
    string ActorUserId,
    string ActorDisplay,
    string TargetUserId,
    string? Reason,
    bool Confirmed,
    string? CorrelationId = null);

// Lenh dong bo lai thanh tich toan he thong theo lo.
public sealed record AdminAchievementBatchSyncCommand(
    string ActorUserId,
    string ActorDisplay,
    string? Reason,
    bool Confirmed,
    int BatchSize,
    string? CorrelationId = null);

// Ket qua dong bo cho mot nguoi dung.
public sealed record AdminAchievementSyncResult(
    bool Succeeded,
    string Message,
    int ChangedCount,
    int FailedCount)
{
    // Tao ket qua thanh cong voi thong bao hien thi tieng Viet.
    public static AdminAchievementSyncResult Success(string message, int changedCount)
    {
        return new AdminAchievementSyncResult(true, message, changedCount, 0);
    }

    // Tao ket qua that bai voi thong bao hien thi tieng Viet.
    public static AdminAchievementSyncResult Failure(string message, int failedCount = 1)
    {
        return new AdminAchievementSyncResult(false, message, 0, failedCount);
    }
}

// Ket qua dong bo toan he thong, co dem so user da xu ly va so thanh tich moi.
public sealed record AdminAchievementBatchSyncResult(
    bool Succeeded,
    string Message,
    int ProcessedUsers,
    int ChangedCount,
    int FailedCount)
{
    // Tao ket qua dong bo toan he thong sau khi da xu ly het cac user trong batch.
    public static AdminAchievementBatchSyncResult FromCounts(
        int processedUsers,
        int changedCount,
        int failedCount)
    {
        if (failedCount > 0)
        {
            return new AdminAchievementBatchSyncResult(
                false,
                $"Da dong bo {processedUsers:N0} nguoi dung, them {changedCount:N0} thanh tich va co {failedCount:N0} loi can kiem tra.",
                processedUsers,
                changedCount,
                failedCount);
        }

        return new AdminAchievementBatchSyncResult(
            true,
            $"Da dong bo {processedUsers:N0} nguoi dung va them {changedCount:N0} thanh tich con thieu.",
            processedUsers,
            changedCount,
            0);
    }

    // Tao ket qua that bai truoc khi tien hanh dong bo.
    public static AdminAchievementBatchSyncResult Failure(string message)
    {
        return new AdminAchievementBatchSyncResult(false, message, 0, 0, 1);
    }
}
