using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Audit;

public sealed class AdminAuditService : IAdminAuditService
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    // Nhận DbContext dùng chung với nghiệp vụ và nguồn thời gian có thể điều khiển trong test.
    public AdminAuditService(AppDbContext context, TimeProvider timeProvider)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    // Thêm audit vào ChangeTracker để caller lưu cùng transaction với thay đổi nghiệp vụ.
    public void Enqueue(AdminAuditEntry entry)
    {
        // 1. Gọi `Add` để thực hiện bước nghiệp vụ này.
        _context.AdminAuditLogs.Add(BuildLog(entry));
    }

    // Ghi ngay một audit độc lập cho các nhánh chỉ đọc hoặc bị từ chối không có transaction nghiệp vụ.
    public async Task<AdminAuditLog> RecordAsync(
        AdminAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `BuildLog` và lưu kết quả vào `log`.
        AdminAuditLog log = BuildLog(entry);
        // 2. Gọi `Add` để thực hiện bước nghiệp vụ này.
        _context.AdminAuditLogs.Add(log);
        // 3. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync(cancellationToken);
        // 4. Trả `log` cho nơi gọi.
        return log;
    }

    // Lọc, sắp xếp và phân trang audit phía máy chủ với giới hạn tối đa 100 dòng.
    public async Task<AdminAuditLogPage> SearchAsync(
        AdminAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `Max` và lưu kết quả vào `page`.
        int page = Math.Max(1, query.Page);
        // 2. Gọi `Clamp` và lưu kết quả vào `pageSize`.
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // 3. Gọi `AsNoTracking` và lưu kết quả vào `logs`.
        IQueryable<AdminAuditLog> logs = _context.AdminAuditLogs.AsNoTracking();

        // 4. Kiểm tra `!string.IsNullOrWhiteSpace(query.Action)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            // 5. Gọi `Trim` và lưu kết quả vào `action`.
            string action = query.Action.Trim();
            // 6. Cập nhật `logs` bằng giá trị mới.
            logs = logs.Where(log => log.Action == action);
        }

        // 7. Kiểm tra `!string.IsNullOrWhiteSpace(query.Outcome)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            // 8. Gọi `Trim` và lưu kết quả vào `outcome`.
            string outcome = query.Outcome.Trim();
            // 9. Cập nhật `logs` bằng giá trị mới.
            logs = logs.Where(log => log.Outcome == outcome);
        }

        // 10. Kiểm tra `!string.IsNullOrWhiteSpace(query.Search)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // 11. Gọi `Trim` và lưu kết quả vào `term`.
            string term = query.Search.Trim();
            // 12. Cập nhật `logs` bằng giá trị mới.
            logs = logs.Where(log =>
                log.ActorDisplay.Contains(term)
                || log.ActorUserId.Contains(term)
                || log.Action.Contains(term)
                || (log.TargetId != null && log.TargetId.Contains(term)));
        }

        // 13. Gọi `CountAsync` và lưu kết quả vào `totalCount`.
        int totalCount = await logs.CountAsync(cancellationToken);
        // 14. Gọi `ToListAsync` và lưu kết quả vào `items`.
        List<AdminAuditLog> items = await logs
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 15. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminAuditLogPage(items, totalCount, page, pageSize);
    }

    // Chuẩn hóa dữ liệu audit và chỉ serialize metadata đã qua allowlist an toàn.
    private AdminAuditLog BuildLog(AdminAuditEntry entry)
    {
        // 1. Gọi `ThrowIfNullOrWhiteSpace` để thực hiện bước nghiệp vụ này.
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ActorUserId);
        // 2. Gọi `ThrowIfNullOrWhiteSpace` để thực hiện bước nghiệp vụ này.
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ActorDisplay);
        // 3. Gọi `ThrowIfNullOrWhiteSpace` để thực hiện bước nghiệp vụ này.
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Action);
        // 4. Gọi `ThrowIfNullOrWhiteSpace` để thực hiện bước nghiệp vụ này.
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Outcome);

        // 5. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminAuditLog
        {
            OccurredAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            ActorUserId = entry.ActorUserId.Trim(),
            ActorDisplay = entry.ActorDisplay.Trim(),
            Action = entry.Action.Trim(),
            TargetType = TrimOrNull(entry.TargetType),
            TargetId = TrimOrNull(entry.TargetId),
            Outcome = entry.Outcome.Trim(),
            Reason = TrimOrNull(entry.Reason),
            CorrelationId = TrimOrNull(entry.CorrelationId),
            MetadataJson = AdminAuditMetadata.Serialize(entry.Metadata)
        };
    }

    // Chuyển chuỗi trống thành null và bỏ khoảng trắng thừa trước khi lưu.
    private static string? TrimOrNull(string? value)
    {
        // 1. Trả `string.IsNullOrWhiteSpace(value) ? null : value.Trim()` cho nơi gọi.
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
