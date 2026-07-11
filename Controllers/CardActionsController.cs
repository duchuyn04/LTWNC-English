using ltwnc.Models.Enums;
using ltwnc.Services;
using ltwnc.Services.CardActions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

[Authorize]
public class CardActionsController : Controller
{
    private readonly CardActionService _cardActionService;
    private readonly FlashcardSetService _setService;
    private readonly UserManager<IdentityUser> _userManager;

    public CardActionsController(
        CardActionService cardActionService,
        FlashcardSetService setService,
        UserManager<IdentityUser> userManager)
    {
        _cardActionService = cardActionService;
        _setService = setService;
        _userManager = userManager;
    }

    [HttpPost]
    [Route("/Set/{setId}/BatchAction")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchAction(int setId, BatchActionType action, List<int> selectedCardIds)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var set = await _setService.GetSetByIdAsync(setId);
        if (set == null || set.UserId != user.Id) return Forbid();

        if (selectedCardIds.Count == 0)
        {
            TempData["Error"] = "Chưa chọn thẻ nào.";
            return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
        }

        try
        {
            ICardActionCommand command = action switch
            {
                BatchActionType.Delete => new DeleteCardsCommand(_cardActionService.Context, setId, user.Id, selectedCardIds),
                BatchActionType.Star => new StarCardsCommand(_cardActionService.Context, setId, user.Id, selectedCardIds),
                BatchActionType.Unstar => new UnstarCardsCommand(_cardActionService.Context, setId, user.Id, selectedCardIds),
                _ => throw new InvalidOperationException("Hành động không hợp lệ.")
            };

            var log = await _cardActionService.ExecuteAsync(command);
            TempData["Success"] = $"Đã {Describe(action)} {selectedCardIds.Count} thẻ.";
            TempData["UndoLogId"] = log.Id;
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
    }

    [HttpPost]
    [Route("/CardActions/Undo/{logId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(int logId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var log = await _cardActionService.GetLogByIdAsync(logId, user.Id);
        if (log == null) return NotFound();

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

    private static string Describe(BatchActionType action) => action switch
    {
        BatchActionType.Delete => "xóa",
        BatchActionType.Star => "đánh sao",
        BatchActionType.Unstar => "bỏ sao",
        _ => "thực hiện"
    };
}
