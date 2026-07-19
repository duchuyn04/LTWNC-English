using ltwnc.Areas.Admin.Models;
using ltwnc.Services.ContentModeration;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

// Kiểm duyệt bộ flashcard: danh sách summary, chi tiết chỉ đọc, cách ly và khôi phục.
[Area("Admin")]
[Route("Admin/Content")]
public sealed class ContentController : Controller
{
    private readonly IContentModerationService _moderationService;

    // Controller chỉ điều phối HTTP; mọi quy tắc kiểm duyệt nằm trong service.
    public ContentController(IContentModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    // Danh sách Admin chỉ hiển thị thông tin khái quát, không mở nội dung thẻ của bộ riêng tư.
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? visibility,
        int page = ContentModerationService.DefaultPage,
        int pageSize = ContentModerationService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new AdminContentSetQuery(
            Search: search,
            Status: status,
            Visibility: visibility,
            Page: page,
            PageSize: pageSize);

        AdminContentSetPage result = await _moderationService.SearchSetsAsync(
            query,
            cancellationToken);
        AdminContentIndexViewModel model =
            AdminContentModerationViewModelMapper.ToIndexViewModel(result, query);

        return View(model);
    }

    // Chi tiết bộ flashcard chỉ đọc; bộ riêng tư bắt buộc nhập lý do trước khi xem danh sách thẻ.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(
        int id,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        AdminContentSetDetailsResult result = await _moderationService.GetDetailsAsync(
            id,
            BuildAccessCommand(reason),
            cancellationToken);
        if (!result.Found)
        {
            return NotFound();
        }

        if (result.RequiresReason)
        {
            AdminContentReasonGateViewModel gate =
                AdminContentModerationViewModelMapper.ToReasonGateViewModel(
                    id,
                    reason,
                    result.Message);
            return View("ReasonGate", gate);
        }

        AdminContentDetailsViewModel model =
            AdminContentModerationViewModelMapper.ToDetailsViewModel(result.Details!);
        return View(model);
    }

    // POST cách ly trực tiếp từ trang Admin Content, bắt buộc xác nhận và lý do công khai.
    [HttpPost("{id:int}/Quarantine")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Quarantine(
        int id,
        AdminQuarantineContentInputModel input,
        CancellationToken cancellationToken = default)
    {
        ContentModerationOperationResult result =
            await _moderationService.QuarantineSetAsync(
                BuildQuarantineCommand(id, input),
                cancellationToken);
        StoreOperationMessage(result);

        return RedirectToAction(nameof(Index));
    }

    // POST khôi phục bộ đã cách ly; chỉ Admin có route này nên tác giả không thể tự khôi phục.
    [HttpPost("{id:int}/Restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(
        int id,
        AdminRestoreContentInputModel input,
        CancellationToken cancellationToken = default)
    {
        ContentModerationOperationResult result =
            await _moderationService.RestoreSetAsync(
                BuildRestoreCommand(id, input),
                cancellationToken);
        StoreOperationMessage(result);

        return RedirectToAction(nameof(Index));
    }

    // Dựng ngữ cảnh truy cập nội dung riêng tư từ Admin hiện tại.
    private AdminContentSetAccessCommand BuildAccessCommand(string? reason)
    {
        return new AdminContentSetAccessCommand(
            Actor: AdminActorContextFactory.FromHttpContext(HttpContext),
            Reason: reason);
    }

    // Dựng lệnh cách ly trực tiếp từ dữ liệu form.
    private QuarantineFlashcardSetCommand BuildQuarantineCommand(
        int flashcardSetId,
        AdminQuarantineContentInputModel input)
    {
        return new QuarantineFlashcardSetCommand(
            FlashcardSetId: flashcardSetId,
            Version: input.Version,
            Actor: AdminActorContextFactory.FromHttpContext(HttpContext),
            PublicReason: input.PublicReason,
            InternalNote: input.InternalNote,
            Evidence: input.Evidence,
            Confirmed: input.Confirmed);
    }

    // Dựng lệnh khôi phục từ dữ liệu form.
    private RestoreFlashcardSetCommand BuildRestoreCommand(
        int flashcardSetId,
        AdminRestoreContentInputModel input)
    {
        return new RestoreFlashcardSetCommand(
            FlashcardSetId: flashcardSetId,
            Version: input.Version,
            Actor: AdminActorContextFactory.FromHttpContext(HttpContext),
            Reason: input.Reason,
            Confirmed: input.Confirmed);
    }

    // Lưu thông báo qua TempData để hiển thị sau PRG.
    private void StoreOperationMessage(ContentModerationOperationResult result)
    {
        if (result.Succeeded)
        {
            TempData["ContentModerationSuccess"] = result.Message;
            return;
        }

        TempData["ContentModerationError"] = result.Message;
    }
}
