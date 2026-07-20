namespace ltwnc.Services.AdminAuditRetention;

public interface IAdminAuditRetentionService
{
    // Dọn audit quá 12 tháng theo batch nhỏ để tác vụ có thể chạy lặp lại an toàn.
    Task<AdminAuditRetentionCleanupResult> CleanupExpiredAuditLogsAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}
