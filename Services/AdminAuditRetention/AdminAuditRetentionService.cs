using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.AdminAuditRetention;

public sealed class AdminAuditRetentionService : IAdminAuditRetentionService
{
    public const int DefaultBatchSize = 500;
    public const int MaxBatchSize = 1_000;
    public const int RetentionMonths = 12;

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    // Nhận DbContext và đồng hồ để cleanup có thể test bằng fake time ở đúng ranh 12 tháng.
    public AdminAuditRetentionService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    // Xóa các bản ghi cũ hơn cutoff theo thứ tự cũ nhất trước; chạy lại sẽ không đụng dữ liệu chưa hết hạn.
    public async Task<AdminAuditRetentionCleanupResult> CleanupExpiredAuditLogsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        int effectiveBatchSize = Math.Clamp(batchSize, 1, MaxBatchSize);
        DateTime cutoffUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMonths(-RetentionMonths);

        List<AdminAuditLog> expiredLogs = await _context.AdminAuditLogs
            .Where(log => log.OccurredAtUtc < cutoffUtc)
            .OrderBy(log => log.OccurredAtUtc)
            .ThenBy(log => log.Id)
            .Take(effectiveBatchSize)
            .ToListAsync(cancellationToken);

        if (expiredLogs.Count > 0)
        {
            _context.AdminAuditLogs.RemoveRange(expiredLogs);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new AdminAuditRetentionCleanupResult(
            expiredLogs.Count,
            cutoffUtc,
            effectiveBatchSize);
    }
}
