using ltwnc.Areas.Admin.Models;
using ltwnc.Models.Entities;
using ltwnc.Services.AdminExports;
using ltwnc.Services.Audit;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/AuditLogs")]
public sealed class AuditLogsController : Controller
{
    private readonly IAdminAuditService _auditService;
    private readonly IAdminExportService _exportService;
    private readonly ICurrentUser _currentUser;

    // Nhận service audit, export và current user để trang danh sách và CSV dùng cùng bộ lọc.
    public AuditLogsController(
        IAdminAuditService auditService,
        IAdminExportService exportService,
        ICurrentUser currentUser)
    {
        _auditService = auditService;
        _exportService = exportService;
        _currentUser = currentUser;
    }

    // Hiển thị danh sách audit theo filter hiện tại, dùng phân trang server-side.
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        [FromQuery(Name = "action")] string? action,
        string? outcome,
        int page = 1,
        int pageSize = AdminAuditService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        AdminAuditLogPage result = await _auditService.SearchAsync(
            new AdminAuditQuery(
                Search: search,
                Action: action,
                Outcome: outcome,
                Page: page,
                PageSize: pageSize),
            cancellationToken);

        var model = new AdminAuditLogIndexViewModel
        {
            Items = result.Items.Select(ToRow).ToArray(),
            Search = search,
            Action = action,
            Outcome = outcome,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount
        };

        return View(model);
    }

    // Xuất CSV audit theo bộ lọc hiện tại, giới hạn 12 tháng gần nhất và số dòng tối đa.
    [HttpGet("Export")]
    public async Task<IActionResult> Export(
        string? search,
        [FromQuery(Name = "action")] string? action,
        string? outcome,
        CancellationToken cancellationToken = default)
    {
        AdminCsvExport export = await _exportService.ExportAuditLogsAsync(
            new AdminAuditExportQuery(search, action, outcome),
            AdminExportActorFactory.FromCurrentUser(_currentUser),
            cancellationToken);

        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        return File(export.Content, "text/csv; charset=utf-8", export.FileName);
    }

    // Chuyển entity audit sang view row gọn, không bung metadata JSON ra UI danh sách.
    private static AdminAuditLogRow ToRow(AdminAuditLog log)
    {
        string target = "—";
        if (log.TargetType != null)
        {
            target = log.TargetType;
            if (log.TargetId != null)
            {
                target = $"{log.TargetType} #{log.TargetId}";
            }
        }

        return new AdminAuditLogRow
        {
            OccurredAtDisplay = AdminTimeZone.ToVietnamTime(log.OccurredAtUtc)
                .ToString("HH:mm:ss dd/MM/yyyy"),
            ActorDisplay = log.ActorDisplay,
            Action = log.Action,
            Target = target,
            Outcome = log.Outcome,
            Reason = log.Reason,
            CorrelationId = log.CorrelationId
        };
    }

}
