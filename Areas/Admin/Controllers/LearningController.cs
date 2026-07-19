using System.Security.Claims;
using ltwnc.Areas.Admin.Models;
using ltwnc.Services.AdminStudyRecords;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

// Hồ sơ học tập ở chế độ CHỈ ĐỌC: không có bất kỳ action POST nào.
// Admin không thể sửa điểm, sửa tiến độ hoặc xóa lịch sử phiên học của ngườ học.
[Area("Admin")]
[Route("Admin/Learning")]
public sealed class LearningController : Controller
{
    private readonly IAdminStudyRecordService _studyRecordService;
    private readonly TimeProvider _timeProvider;

    // Controller chỉ điều phối HTTP; toàn bộ nghiệp vụ nằm ở tầng service.
    public LearningController(
        IAdminStudyRecordService studyRecordService,
        TimeProvider timeProvider)
    {
        _studyRecordService = studyRecordService;
        _timeProvider = timeProvider;
    }

    // Danh sách phiên học với tìm kiếm, lọc, sắp xếp và phân trang phía máy chủ.
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        string? userId,
        string? mode,
        string? status,
        DateOnly? from,
        DateOnly? to,
        string? sort,
        int page = AdminStudyRecordService.DefaultPage,
        int pageSize = AdminStudyRecordService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new AdminStudySessionQuery(
            Search: search,
            UserId: userId,
            Mode: mode,
            Status: status,
            From: from,
            To: to,
            Sort: sort,
            Page: page,
            PageSize: pageSize);

        AdminStudySessionPage result =
            await _studyRecordService.SearchAsync(query, cancellationToken);
        AdminLearningIndexViewModel model = AdminLearningViewModelMapper.ToIndexViewModel(
            result,
            query,
            _timeProvider.GetUtcNow());

        return View(model);
    }

    // Chi tiết một phiên học cấp ngườ học.
    // Không có lý do hỗ trợ/điều tra thì chỉ hiển thị cổng nhập lý do;
    // có lý do thì service ghi audit thành công trước rồi mới trả dữ liệu.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(
        int id,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        // Bước 1: chưa có lý do thì dừng ở cổng, không truy vấn dữ liệu phiên.
        if (string.IsNullOrWhiteSpace(reason))
        {
            return View(
                "ReasonGate",
                AdminLearningViewModelMapper.ToReasonGateViewModel(id, reason, null));
        }

        // Bước 2: lý do quá dài cũng quay lại cổng kèm thông báo lỗi.
        if (reason.Trim().Length > 500)
        {
            return View(
                "ReasonGate",
                AdminLearningViewModelMapper.ToReasonGateViewModel(
                    id,
                    reason,
                    "Lý do không được vượt quá 500 ký tự."));
        }

        // Bước 3: service ghi Bản ghi kiểm toán truy cập nhạy cảm trước khi trả dữ liệu;
        // nếu audit thất bại thì yêu cầu thất bại theo (không trả dữ liệu).
        AdminStudySessionDetails? details = await _studyRecordService.GetDetailsAsync(
            id,
            BuildAccessCommand(reason),
            cancellationToken);
        if (details == null)
        {
            return NotFound();
        }

        AdminLearningDetailsViewModel model = AdminLearningViewModelMapper.ToDetailsViewModel(
            details,
            reason.Trim(),
            _timeProvider.GetUtcNow());
        return View(model);
    }

    // Dựng ngữ cảnh truy cập nhạy cảm từ ngườ đang đăng nhập và lý do đã nhập.
    private AdminStudyRecordAccessCommand BuildAccessCommand(string reason)
    {
        string actorUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        string actorDisplay = User.Identity?.Name ?? actorUserId;

        return new AdminStudyRecordAccessCommand(
            ActorUserId: actorUserId,
            ActorDisplay: actorDisplay,
            Reason: reason,
            CorrelationId: HttpContext.TraceIdentifier);
    }
}
