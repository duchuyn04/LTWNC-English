using ltwnc.Models.Enums;
using ltwnc.Models.Entities;
using ltwnc.Services.Auth;
using ltwnc.Services.CardActions;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// Batch trên thẻ (xóa / sao / bỏ sao) và Undo. Chỉ chủ bộ thẻ.
[Authorize]
public class CardActionsController : Controller
{
    // Execute + Undo + đọc log
    private readonly ICardActionService _cardActionService;

    // Map BatchActionType -> command
    private readonly ICardActionCommandFactory _commandFactory;

    // Kiểm tra set tồn tại / owner
    private readonly IFlashcardSetService _setService;

    // User hiện tại từ cookie claims
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CardActionsController> _logger;

    public CardActionsController(
        ICardActionService cardActionService,
        ICardActionCommandFactory commandFactory,
        IFlashcardSetService setService,
        ICurrentUser currentUser,
        ILogger<CardActionsController> logger)
    {
        _cardActionService = cardActionService;
        _commandFactory = commandFactory;
        _setService = setService;
        _currentUser = currentUser;
        _logger = logger;
    }

    // POST batch: factory tạo command, Execute, TempData success + UndoLogId
    [HttpPost]
    [Route("/Set/{setId}/BatchAction")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchAction(
        int setId,
        BatchActionType action,
        List<int> selectedCardIds)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        FlashcardSet? set = await _setService.GetSetByIdAsync(setId);
        if (set == null || set.UserId != userId)
        {
            return Forbid();
        }

        if (selectedCardIds.Count == 0)
        {
            const string message = "Chưa chọn thẻ nào.";
            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false, message });
            }

            TempData["Error"] = message;
            return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
        }

        try
        {
            // action.ToString() khớp factory: "Delete", "Star", "Unstar"
            ICardActionCommand command = _commandFactory.Create(
                action.ToString(),
                setId,
                userId,
                selectedCardIds);

            CardActionLog log = await _cardActionService.ExecuteAsync(command);
            string message = $"Đã {Describe(action)} {selectedCardIds.Count} thẻ.";
            if (IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    message,
                    action = action.ToString(),
                    cardIds = selectedCardIds,
                    undoLogId = log.Id
                });
            }

            TempData["Success"] = message;
            TempData["UndoLogId"] = log.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch card action failed for set {SetId}.", setId);
            const string safeMessage = "Không thể thực hiện thao tác. Vui lòng thử lại.";
            if (IsAjaxRequest())
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { success = false, message = safeMessage });
            }

            TempData["Error"] = safeMessage;
        }

        return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
    }

    private bool IsAjaxRequest()
    {
        if (string.Equals(
                Request.Headers.XRequestedWith,
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Request.Headers.TryGetValue("Accept", out Microsoft.Extensions.Primitives.StringValues accept)
            && accept.ToString().Contains(
                "application/json",
                StringComparison.OrdinalIgnoreCase);
    }

    // POST Undo theo logId của user; redirect về Edit set của log
    [HttpPost]
    [Route("/CardActions/Undo/{logId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(int logId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        CardActionLog? log = await _cardActionService.GetLogByIdAsync(logId, userId);
        if (log == null)
        {
            return NotFound();
        }

        try
        {
            await _cardActionService.UndoAsync(logId, userId);
            TempData["Success"] = "Đã hoàn tác hành động.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Undo card action failed for log {LogId}.", logId);
            TempData["Error"] = "Không thể hoàn tác. Vui lòng thử lại.";
        }

        return RedirectToAction("Edit", "FlashcardSet", new { id = log.SetId });
    }

    // Enum action -> động từ tiếng Việt cho thông báo
    private static string Describe(BatchActionType action)
    {
        switch (action)
        {
            case BatchActionType.Delete:
                return "xóa";
            case BatchActionType.Star:
                return "đánh sao";
            case BatchActionType.Unstar:
                return "bỏ sao";
            default:
                return "thực hiện";
        }
    }
}
