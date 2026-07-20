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
        _context = context;
        _kpiService = kpiService;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    // Xuất KPI theo đúng bộ lọc ngày của dashboard, chỉ gồm số liệu tổng hợp không chứa dữ liệu cá nhân.
    public async Task<AdminCsvExport> ExportKpisAsync(
        int? days,
        AdminExportActor actor,
        CancellationToken cancellationToken = default)
    {
        AdminDashboardSnapshot snapshot = await _kpiService.GetSnapshotAsync(days, cancellationToken);
        AdminDashboardViewModel viewModel = AdminDashboardKpiService.ToViewModel(snapshot);

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

        byte[] content = SafeCsvWriter.Write(
            ["Metric", "Value", "Detail", "Comparison", "Tone"],
            rows);

        await RecordExportAuditAsync(
            actor,
            KpiExportType,
            $"days={viewModel.Days}",
            rows.Count,
            cancellationToken);

        string fileName = $"admin-kpi-{viewModel.Days}-days-{FormatDateStamp()}.csv";
        return new AdminCsvExport(fileName, content, rows.Count);
    }

    // Xuất audit theo search/action/outcome hiện tại, tự giới hạn trong 12 tháng gần nhất và cap số dòng.
    public async Task<AdminCsvExport> ExportAuditLogsAsync(
        AdminAuditExportQuery query,
        AdminExportActor actor,
        CancellationToken cancellationToken = default)
    {
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime cutoffUtc = nowUtc.AddMonths(-AuditExportRetentionMonths);
        IQueryable<AdminAuditLog> logs = ApplyAuditFilters(
            _context.AdminAuditLogs.AsNoTracking(),
            query,
            cutoffUtc,
            nowUtc);

        List<AdminAuditLog> items = await logs
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Take(AuditExportMaxRows)
            .ToListAsync(cancellationToken);

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

        byte[] content = SafeCsvWriter.Write(
            ["OccurredAtVietnam", "Actor", "Action", "Target", "Outcome"],
            rows);

        await RecordExportAuditAsync(
            actor,
            AuditExportType,
            BuildAuditFilterSummary(query, cutoffUtc, nowUtc),
            rows.Count,
            cancellationToken);

        string fileName = $"admin-audit-logs-{FormatDateStamp()}.csv";
        return new AdminCsvExport(fileName, content, rows.Count);
    }

    // Áp bộ lọc audit giống trang danh sách và thêm ranh thời gian cố định cho export.
    private static IQueryable<AdminAuditLog> ApplyAuditFilters(
        IQueryable<AdminAuditLog> logs,
        AdminAuditExportQuery query,
        DateTime cutoffUtc,
        DateTime nowUtc)
    {
        logs = logs.Where(log => log.OccurredAtUtc >= cutoffUtc && log.OccurredAtUtc <= nowUtc);

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            string action = query.Action.Trim();
            logs = logs.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            string outcome = query.Outcome.Trim();
            logs = logs.Where(log => log.Outcome == outcome);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            logs = logs.Where(log =>
                log.ActorDisplay.Contains(term)
                || log.ActorUserId.Contains(term)
                || log.Action.Contains(term)
                || (log.TargetId != null && log.TargetId.Contains(term)));
        }

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
        List<string> parts =
        [
            $"fromUtc={cutoffUtc:O}",
            $"toUtc={nowUtc:O}"
        ];

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parts.Add($"search={query.Search.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            parts.Add($"action={query.Action.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            parts.Add($"outcome={query.Outcome.Trim()}");
        }

        parts.Add($"maxRows={AuditExportMaxRows}");
        return string.Join(";", parts);
    }

    // Dựng target audit gọn giống UI nhưng không mở rộng metadata riêng tư.
    private static string BuildTarget(AdminAuditLog log)
    {
        if (log.TargetType == null)
        {
            return string.Empty;
        }

        if (log.TargetId == null)
        {
            return log.TargetType;
        }

        return $"{log.TargetType} #{log.TargetId}";
    }

    // Tạo dấu ngày UTC cho tên file ổn định trong test và vận hành.
    private string FormatDateStamp()
    {
        return _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss");
    }
}
