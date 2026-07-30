using ltwnc.Areas.Admin.Models;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

// Tra cứu và xuất lịch sử thao tác quản trị để phục vụ kiểm tra, truy vết.
[Area("Admin")]
[Route("Admin/AuditLogs")]
public sealed class AuditLogsController : Controller
{
    private readonly IAdminAuditService _auditService;

    // Nhận service audit, export và current user để trang danh sách và CSV dùng cùng bộ lọc.
    public AuditLogsController(IAdminAuditService auditService)
    {
        _auditService = auditService;
    }

    // Hiển thị danh sách audit theo filter hiện tại, dùng phân trang server-side.
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        string? outcome,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        AdminAuditLogPage result = await _auditService.SearchAsync(
            new AdminAuditQuery(
                Search: search,
                Outcome: outcome,
                Page: page),
            cancellationToken);

        var model = new AdminAuditLogIndexViewModel
        {
            Items = result.Items.Select(ToRow).ToArray(),
            Search = search,
            Outcome = outcome,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount
        };

        return View(model);
    }

    // Xuất CSV audit theo bộ lọc hiện tại, giới hạn 12 tháng gần nhất và số dòng tối đa.
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
