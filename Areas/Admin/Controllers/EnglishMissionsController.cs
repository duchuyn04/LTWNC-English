using System.Security.Claims;
using ltwnc.Areas.Admin.Models;
using ltwnc.Services.AdminEnglishMissions;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/EnglishMissions")]
public sealed class EnglishMissionsController : Controller
{
    private readonly IAdminEnglishMissionService _missionService;

    // Controller chỉ điều phối HTTP; mọi ràng buộc quyền riêng tư nằm trong service.
    public EnglishMissionsController(IAdminEnglishMissionService missionService)
    {
        _missionService = missionService;
    }

    // Danh sách summary có lọc/sắp xếp/phân trang server-side, không tải hội thoại.
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        string? topic,
        string? status,
        string? retention,
        string? sort,
        int page = AdminEnglishMissionService.DefaultPage,
        int pageSize = AdminEnglishMissionService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new AdminEnglishMissionQuery(
            Search: search,
            Topic: topic,
            Status: status,
            Retention: retention,
            Sort: sort,
            Page: page,
            PageSize: pageSize);
        AdminEnglishMissionPage result =
            await _missionService.SearchAsync(query, cancellationToken);

        return View(AdminEnglishMissionViewModelMapper.ToIndexViewModel(result, query));
    }

    // Mở hội thoại chi tiết chỉ khi có loại vụ việc, mã tham chiếu tùy chọn và lý do hợp lệ.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(
        int id,
        string? incidentType,
        string? caseReference,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        AdminEnglishMissionConversationResult result =
            await _missionService.GetConversationAsync(
                BuildCommand(id, incidentType, caseReference, reason),
                cancellationToken);
        if (!result.Found)
        {
            return NotFound();
        }

        if (result.RequiresGate)
        {
            return View("ReasonGate",
                AdminEnglishMissionViewModelMapper.ToGateViewModel(
                    id,
                    incidentType,
                    caseReference,
                    reason,
                    result.Message));
        }

        return View(AdminEnglishMissionViewModelMapper.ToDetailsViewModel(
            result.Conversation!));
    }

    // Dựng command truy cập hội thoại từ người dùng Admin hiện tại và query string.
    private AdminEnglishMissionAccessCommand BuildCommand(
        int missionId,
        string? incidentType,
        string? caseReference,
        string? reason)
    {
        string actorUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        string actorDisplay = User.Identity?.Name ?? actorUserId;

        return new AdminEnglishMissionAccessCommand(
            MissionId: missionId,
            ActorUserId: actorUserId,
            ActorDisplay: actorDisplay,
            IncidentType: incidentType ?? string.Empty,
            CaseReference: caseReference,
            Reason: reason ?? string.Empty,
            CorrelationId: HttpContext.TraceIdentifier);
    }
}
