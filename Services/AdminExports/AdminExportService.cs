using ltwnc.Areas.Admin;
using ltwnc.Areas.Admin.Models;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.AdminDashboard;
using ltwnc.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.AdminExports;

public sealed class AdminExportService : IAdminExportService
{
    public const int AuditExportMaxRows = 1_000;
    public const int AuditExportRetentionMonths = 12;

    private const string KpiExportType = "kpi";
    private const string AuditExportType = "audit";
    private const string ExportTypeMetadataKey = "exportType";
    private const string ScopeMetadataKey = "scope";
    private const string FilterMetadataKey = "filter";
    private const string RowCountMetadataKey = "rowCount";
    private const string CountMetadataKey = "count";

    private readonly AppDbContext _context;
    private readonly IAdminDashboardKpiService _kpiService;
    private readonly IAdminAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    // Nhận các service đọc dữ liệu tổng hợp và audit để mọi export đều ghi dấu vết trước khi trả file.
    public AdminExportService(
        AppDbContext context,
        IAdminDashboardKpiService kpiService,
        IAdminAuditService auditService,
        TimeProvider timeProvider)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_kpiService` để các phương thức khác sử dụng.
        _kpiService = kpiService;
        // 3. Lưu dependency `_auditService` để các phương thức khác sử dụng.
        _auditService = auditService;
        // 4. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    // Xuất KPI theo đúng bộ lọc ngày của dashboard, chỉ gồm số liệu tổng hợp không chứa dữ liệu cá nhân.
    public async Task<AdminCsvExport> ExportKpisAsync(
        int? days,
        AdminExportActor actor,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `GetSnapshotAsync` và lưu kết quả vào `snapshot`.
        AdminDashboardSnapshot snapshot = await _kpiService.GetSnapshotAsync(days, cancellationToken);
        // 2. Gọi `ToViewModel` và lưu kết quả vào `viewModel`.
        AdminDashboardViewModel viewModel = AdminDashboardKpiService.ToViewModel(snapshot);

        // 3. Gọi `ToList` và lưu kết quả vào `rows`.
        List<IReadOnlyList<string?>> rows = viewModel.Kpis
            .Select(kpi => (IReadOnlyList<string?>)
            [
                kpi.Label,
                kpi.Value,
                kpi.Detail,
                kpi.Comparison,
                kpi.Tone
            ])
            .ToList();

        // 4. Gọi `Write` và lưu kết quả vào `content`.
        byte[] content = SafeCsvWriter.Write(
            ["Metric", "Value", "Detail", "Comparison", "Tone"],
            rows);

        // 5. Gọi `RecordExportAuditAsync` để thực hiện bước nghiệp vụ này.
        await RecordExportAuditAsync(
            actor,
            KpiExportType,
            $"days={viewModel.Days}",
            rows.Count,
            cancellationToken);

        // 6. Tính giá trị và lưu vào `fileName` để dùng ở bước tiếp theo.
        string fileName = $"admin-kpi-{viewModel.Days}-days-{FormatDateStamp()}.csv";
        // 7. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminCsvExport(fileName, content, rows.Count);
    }

    // Xuất audit theo search/action/outcome hiện tại, tự giới hạn trong 12 tháng gần nhất và cap số dòng.
    public async Task<AdminCsvExport> ExportAuditLogsAsync(
        AdminAuditExportQuery query,
        AdminExportActor actor,
        CancellationToken cancellationToken = default)
    {
        // 1. Tính giá trị và lưu vào `nowUtc` để dùng ở bước tiếp theo.
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        // 2. Gọi `AddMonths` và lưu kết quả vào `cutoffUtc`.
        DateTime cutoffUtc = nowUtc.AddMonths(-AuditExportRetentionMonths);
        // 3. Gọi `ApplyAuditFilters` và lưu kết quả vào `logs`.
        IQueryable<AdminAuditLog> logs = ApplyAuditFilters(
            _context.AdminAuditLogs.AsNoTracking(),
            query,
            cutoffUtc,
            nowUtc);

        // 4. Gọi `ToListAsync` và lưu kết quả vào `items`.
        List<AdminAuditLog> items = await logs
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Take(AuditExportMaxRows)
            .ToListAsync(cancellationToken);

        // 5. Gọi `ToList` và lưu kết quả vào `rows`.
        List<IReadOnlyList<string?>> rows = items
            .Select(log => (IReadOnlyList<string?>)
            [
                AdminTimeZone.ToVietnamTime(log.OccurredAtUtc).ToString("yyyy-MM-dd HH:mm:ss zzz"),
                log.ActorDisplay,
                log.Action,
                BuildTarget(log),
                log.Outcome
            ])
            .ToList();

        // 6. Gọi `Write` và lưu kết quả vào `content`.
        byte[] content = SafeCsvWriter.Write(
            ["OccurredAtVietnam", "Actor", "Action", "Target", "Outcome"],
            rows);

        // 7. Gọi `RecordExportAuditAsync` để thực hiện bước nghiệp vụ này.
        await RecordExportAuditAsync(
            actor,
            AuditExportType,
            BuildAuditFilterSummary(query, cutoffUtc, nowUtc),
            rows.Count,
            cancellationToken);

        // 8. Tính giá trị và lưu vào `fileName` để dùng ở bước tiếp theo.
        string fileName = $"admin-audit-logs-{FormatDateStamp()}.csv";
        // 9. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminCsvExport(fileName, content, rows.Count);
    }

    // Áp bộ lọc audit giống trang danh sách và thêm ranh thời gian cố định cho export.
    private static IQueryable<AdminAuditLog> ApplyAuditFilters(
        IQueryable<AdminAuditLog> logs,
        AdminAuditExportQuery query,
        DateTime cutoffUtc,
        DateTime nowUtc)
    {
        // 1. Cập nhật `logs` bằng giá trị mới.
        logs = logs.Where(log => log.OccurredAtUtc >= cutoffUtc && log.OccurredAtUtc <= nowUtc);

        // 2. Kiểm tra `!string.IsNullOrWhiteSpace(query.Action)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            // 3. Gọi `Trim` và lưu kết quả vào `action`.
            string action = query.Action.Trim();
            // 4. Cập nhật `logs` bằng giá trị mới.
            logs = logs.Where(log => log.Action == action);
        }

        // 5. Kiểm tra `!string.IsNullOrWhiteSpace(query.Outcome)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            // 6. Gọi `Trim` và lưu kết quả vào `outcome`.
            string outcome = query.Outcome.Trim();
            // 7. Cập nhật `logs` bằng giá trị mới.
            logs = logs.Where(log => log.Outcome == outcome);
        }

        // 8. Kiểm tra `!string.IsNullOrWhiteSpace(query.Search)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // 9. Gọi `Trim` và lưu kết quả vào `term`.
            string term = query.Search.Trim();
            // 10. Cập nhật `logs` bằng giá trị mới.
            logs = logs.Where(log =>
                log.ActorDisplay.Contains(term)
                || log.ActorUserId.Contains(term)
                || log.Action.Contains(term)
                || (log.TargetId != null && log.TargetId.Contains(term)));
        }

        // 11. Trả `logs` cho nơi gọi.
        return logs;
    }

    // Ghi audit export chỉ chứa loại export, bộ lọc và số dòng, tuyệt đối không ghi dữ liệu đã xuất.
    private async Task RecordExportAuditAsync(
        AdminExportActor actor,
        string exportType,
        string filter,
        int rowCount,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
        await _auditService.RecordAsync(new AdminAuditEntry(
            actor.UserId,
            actor.DisplayName,
            AdminAuditActions.AdminExportsCreate,
            AdminAuditOutcome.Success,
            TargetType: "AdminExport",
            TargetId: exportType,
            Metadata: new Dictionary<string, string?>
            {
                [ExportTypeMetadataKey] = exportType,
                [ScopeMetadataKey] = exportType,
                [FilterMetadataKey] = filter,
                [RowCountMetadataKey] = rowCount.ToString(),
                [CountMetadataKey] = rowCount.ToString()
            }), cancellationToken);
    }

    // Tạo chuỗi filter ngắn đủ quan sát thao tác nhưng không chứa dữ liệu xuất.
    private static string BuildAuditFilterSummary(
        AdminAuditExportQuery query,
        DateTime cutoffUtc,
        DateTime nowUtc)
    {
        // 1. Khởi tạo `parts` với dữ liệu ban đầu cần thiết.
        List<string> parts =
        [
            $"fromUtc={cutoffUtc:O}",
            $"toUtc={nowUtc:O}"
        ];

        // 2. Kiểm tra `!string.IsNullOrWhiteSpace(query.Search)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // 3. Gọi `Add` để thực hiện bước nghiệp vụ này.
            parts.Add($"search={query.Search.Trim()}");
        }

        // 4. Kiểm tra `!string.IsNullOrWhiteSpace(query.Action)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            // 5. Gọi `Add` để thực hiện bước nghiệp vụ này.
            parts.Add($"action={query.Action.Trim()}");
        }

        // 6. Kiểm tra `!string.IsNullOrWhiteSpace(query.Outcome)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            // 7. Gọi `Add` để thực hiện bước nghiệp vụ này.
            parts.Add($"outcome={query.Outcome.Trim()}");
        }

        // 8. Gọi `Add` để thực hiện bước nghiệp vụ này.
        parts.Add($"maxRows={AuditExportMaxRows}");
        // 9. Trả kết quả từ `Join` cho nơi gọi.
        return string.Join(";", parts);
    }

    // Dựng target audit gọn giống UI nhưng không mở rộng metadata riêng tư.
    private static string BuildTarget(AdminAuditLog log)
    {
        // 1. Kiểm tra `log.TargetType == null` để chọn nhánh xử lý phù hợp.
        if (log.TargetType == null)
        {
            // 2. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // 3. Kiểm tra `log.TargetId == null` để chọn nhánh xử lý phù hợp.
        if (log.TargetId == null)
        {
            // 4. Trả `log.TargetType` cho nơi gọi.
            return log.TargetType;
        }

        // 5. Trả `$"{log.TargetType} #{log.TargetId}"` cho nơi gọi.
        return $"{log.TargetType} #{log.TargetId}";
    }

    // Tạo dấu ngày UTC cho tên file ổn định trong test và vận hành.
    private string FormatDateStamp()
    {
        // 1. Trả kết quả từ `ToString` cho nơi gọi.
        return _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss");
    }
}
