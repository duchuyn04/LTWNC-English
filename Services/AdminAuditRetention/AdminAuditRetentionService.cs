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
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    // Xóa các bản ghi cũ hơn cutoff theo thứ tự cũ nhất trước; chạy lại sẽ không đụng dữ liệu chưa hết hạn.
    public async Task<AdminAuditRetentionCleanupResult> CleanupExpiredAuditLogsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `Clamp` và lưu kết quả vào `effectiveBatchSize`.
        int effectiveBatchSize = Math.Clamp(batchSize, 1, MaxBatchSize);
        // 2. Gọi `AddMonths` và lưu kết quả vào `cutoffUtc`.
        DateTime cutoffUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMonths(-RetentionMonths);

        // 3. Gọi `ToListAsync` và lưu kết quả vào `expiredLogs`.
        List<AdminAuditLog> expiredLogs = await _context.AdminAuditLogs
            .Where(log => log.OccurredAtUtc < cutoffUtc)
            .OrderBy(log => log.OccurredAtUtc)
            .ThenBy(log => log.Id)
            .Take(effectiveBatchSize)
            .ToListAsync(cancellationToken);

        // 4. Kiểm tra `expiredLogs.Count > 0` để chọn nhánh xử lý phù hợp.
        if (expiredLogs.Count > 0)
        {
            // 5. Gọi `RemoveRange` để thực hiện bước nghiệp vụ này.
            _context.AdminAuditLogs.RemoveRange(expiredLogs);
            // 6. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
        }

        // 7. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminAuditRetentionCleanupResult(
            expiredLogs.Count,
            cutoffUtc,
            effectiveBatchSize);
    }
}
