using ltwnc.Areas.Admin.Models;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/AuditLogs")]
public sealed class AuditLogsController : Controller
{
    private readonly IAdminAuditService _auditService;

    public AuditLogsController(IAdminAuditService auditService)
    {
        _auditService = auditService;
    }

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

    private static AdminAuditLogRow ToRow(AdminAuditLog log)
    {
        string target = log.TargetType == null
            ? "—"
            : log.TargetId == null
                ? log.TargetType
                : $"{log.TargetType} #{log.TargetId}";

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
