using ltwnc.Models.Enums;
using ltwnc.Models.Entities;
using ltwnc.Services.Auth;
using ltwnc.Services.CardActions;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// Xử lý hàng loạt thẻ (xóa, đánh sao, bỏ sao) và hoàn tác; chỉ chủ bộ thẻ được phép dùng.
[Authorize]
public class CardActionsController : Controller
{
    // Thực thi thao tác, hoàn tác và đọc lịch sử thao tác.
    private readonly ICardActionService _cardActionService;

    // Chuyển loại thao tác thành command nghiệp vụ tương ứng.
    private readonly ICardActionCommandFactory _commandFactory;

    // Kiểm tra bộ thẻ tồn tại và thuộc sở hữu của người dùng.
    private readonly IFlashcardSetService _setService;

    // Đọc người dùng hiện tại và ghi lỗi kỹ thuật khi thao tác thất bại.
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CardActionsController> _logger;

    // Nhận các service cần dùng qua dependency injection.
    public CardActionsController(
        ICardActionService cardActionService,
        ICardActionCommandFactory commandFactory,
        IFlashcardSetService setService,
        ICurrentUser currentUser,
        ILogger<CardActionsController> logger)
    {
        // 1. Lưu các service và logger để các action sử dụng.
        _cardActionService = cardActionService;
        _commandFactory = commandFactory;
        _setService = setService;
        _currentUser = currentUser;
        _logger = logger;
    }

    // Thực hiện một thao tác cho nhiều thẻ và lưu mã lịch sử để có thể hoàn tác.
    [HttpPost]
    [Route("/Set/{setId}/BatchAction")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchAction(
        int setId,
        BatchActionType action,
        List<int> selectedCardIds)
    {
        // 1. Xác định người dùng đang thực hiện thao tác.
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        // 2. Chỉ cho phép chủ sở hữu thao tác trên bộ thẻ.
        FlashcardSet? set = await _setService.GetSetByIdAsync(setId);
        if (set == null || set.UserId != userId)
        {
            return Forbid();
        }

        // 3. Trả lỗi phù hợp nếu người dùng chưa chọn thẻ nào.
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

        // 4. Tạo command, thực thi và nhận mã lịch sử dùng cho hoàn tác.
        try
        {
            // Tên enum khớp với các command mà factory hỗ trợ: Delete, Star và Unstar.
            ICardActionCommand command = _commandFactory.Create(
                action.ToString(),
                setId,
                userId,
                selectedCardIds);

            CardActionLog log = await _cardActionService.ExecuteAsync(command);
            string message = $"Đã {Describe(action)} {selectedCardIds.Count} thẻ.";

            // 5. Yêu cầu AJAX nhận JSON; form thường nhận thông báo qua TempData.
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
            // 6. Ghi lỗi kỹ thuật nhưng chỉ trả thông báo an toàn cho người dùng.
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

        // 7. Sau form POST, quay về trang chỉnh sửa bộ thẻ.
        return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
    }

    // Nhận biết yêu cầu AJAX để trả JSON thay vì chuyển hướng sang trang khác.
    private bool IsAjaxRequest()
    {
        // 1. Ưu tiên header X-Requested-With thường được trình duyệt gửi cho AJAX.
        if (string.Equals(
                Request.Headers.XRequestedWith,
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Nếu thiếu header trên, kiểm tra client có yêu cầu dữ liệu JSON hay không.
        return Request.Headers.TryGetValue("Accept", out Microsoft.Extensions.Primitives.StringValues accept)
            && accept.ToString().Contains(
                "application/json",
                StringComparison.OrdinalIgnoreCase);
    }

    // Hoàn tác theo mã lịch sử của người dùng rồi quay về trang sửa bộ thẻ.
    [HttpPost]
    [Route("/CardActions/Undo/{logId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(int logId)
    {
        // 1. Xác định người dùng hiện tại.
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        // 2. Chỉ lấy lịch sử thao tác thuộc đúng người dùng.
        CardActionLog? log = await _cardActionService.GetLogByIdAsync(logId, userId);
        if (log == null)
        {
            return NotFound();
        }

        // 3. Thực hiện hoàn tác và lưu thông báo kết quả.
        try
        {
            await _cardActionService.UndoAsync(logId, userId);
            TempData["Success"] = "Đã hoàn tác hành động.";
        }
        catch (Exception ex)
        {
            // 4. Ghi lỗi kỹ thuật nếu không thể hoàn tác.
            _logger.LogError(ex, "Undo card action failed for log {LogId}.", logId);
            TempData["Error"] = "Không thể hoàn tác. Vui lòng thử lại.";
        }

        // 5. Quay về đúng bộ thẻ được ghi trong lịch sử thao tác.
        return RedirectToAction("Edit", "FlashcardSet", new { id = log.SetId });
    }

    // Chuyển loại thao tác thành cụm từ tiếng Việt dùng trong thông báo.
    private static string Describe(BatchActionType action)
    {
        // 1. Ánh xạ từng giá trị enum sang động từ dùng trong thông báo.
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
