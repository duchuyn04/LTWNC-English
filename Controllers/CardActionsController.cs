using ltwnc.Models.Enums;
using ltwnc.Models.Entities;
using ltwnc.Services;
using ltwnc.Services.CardActions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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

    // User hiện tại
    private readonly UserManager<IdentityUser> _userManager;

    // Inject service action, factory, set service, UserManager
    public CardActionsController(
        ICardActionService cardActionService,
        ICardActionCommandFactory commandFactory,
        IFlashcardSetService setService,
        UserManager<IdentityUser> userManager)
    {
        _cardActionService = cardActionService;
        _commandFactory = commandFactory;
        _setService = setService;
        _userManager = userManager;
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
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        FlashcardSet? set = await _setService.GetSetByIdAsync(setId);
        if (set == null || set.UserId != user.Id)
        {
            return Forbid();
        }

        if (selectedCardIds.Count == 0)
        {
            TempData["Error"] = "Chưa chọn thẻ nào.";
            return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
        }

        try
        {
            // action.ToString() khớp factory: "Delete", "Star", "Unstar"
            ICardActionCommand command = _commandFactory.Create(
                action.ToString(),
                setId,
                user.Id,
                selectedCardIds);

            CardActionLog log = await _cardActionService.ExecuteAsync(command);
            TempData["Success"] = $"Đã {Describe(action)} {selectedCardIds.Count} thẻ.";
            TempData["UndoLogId"] = log.Id;
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
    }

    // POST Undo theo logId của user; redirect về Edit set của log
    [HttpPost]
    [Route("/CardActions/Undo/{logId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(int logId)
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        CardActionLog? log = await _cardActionService.GetLogByIdAsync(logId, user.Id);
        if (log == null)
        {
            return NotFound();
        }

        try
        {
            await _cardActionService.UndoAsync(logId, user.Id);
            TempData["Success"] = "Đã hoàn tác hành động.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
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
