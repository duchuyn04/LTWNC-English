using ltwnc.Models.Enums;
using ltwnc.Services;
using ltwnc.Services.CardActions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// Controller xử lý các thao tác hàng loạt trên thẻ: xóa, đánh sao, bỏ sao, hoàn tác
// Mọi action đều yêu cầu đăng nhập và kiểm tra quyền chủ sở hữu bộ thẻ
[Authorize]
public class CardActionsController : Controller
{
    private readonly CardActionService _cardActionService;
    private readonly CardActionCommandFactory _commandFactory;
    private readonly FlashcardSetService _setService;
    private readonly UserManager<IdentityUser> _userManager;

    public CardActionsController(
        CardActionService cardActionService,
        CardActionCommandFactory commandFactory,
        FlashcardSetService setService,
        UserManager<IdentityUser> userManager)
    {
        _cardActionService = cardActionService;
        _commandFactory = commandFactory;
        _setService = setService;
        _userManager = userManager;
    }

    // Thực hiện hành động hàng loạt trên các thẻ đã chọn (xóa / đánh sao / bỏ sao)
    [HttpPost]
    [Route("/Set/{setId}/BatchAction")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchAction(int setId, BatchActionType action, List<int> selectedCardIds)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Chỉ chủ sở hữu bộ thẻ mới được thao tác
        var set = await _setService.GetSetByIdAsync(setId);
        if (set == null || set.UserId != user.Id) return Forbid();

        if (selectedCardIds.Count == 0)
        {
            TempData["Error"] = "Chưa chọn thẻ nào.";
            return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
        }

        try
        {
            // Tạo command tương ứng và thực thi trong transaction
            var command = _commandFactory.Create(
                action.ToString(),
                setId,
                user.Id,
                selectedCardIds);

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

    // Hoàn tác một hành động hàng loạt dựa trên log đã ghi
    [HttpPost]
    [Route("/CardActions/Undo/{logId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(int logId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Chỉ cho phép hoàn tác log của chính user hiện tại
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

    // Chuyển action enum thành động từ tiếng Việt để hiển thị thông báo
    private static string Describe(BatchActionType action) => action switch
    {
        BatchActionType.Delete => "xóa",
        BatchActionType.Star => "đánh sao",
        BatchActionType.Unstar => "bỏ sao",
        _ => "thực hiện"
    };
}
