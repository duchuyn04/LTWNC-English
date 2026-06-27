using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Controllers;

// Controller xử lý chức năng học flashcard — yêu cầu đăng nhập
[Authorize]
public class StudyController : Controller
{
    private readonly IStudyService _studyService;
    private readonly IFlashcardSetService _setService;
    private readonly IAccountService _accountService;

    // Inject các service: học tập, bộ thẻ, tài khoản
    public StudyController(IStudyService studyService, IFlashcardSetService setService, IAccountService accountService)
    {
        _studyService = studyService;
        _setService = setService;
        _accountService = accountService;
    }

    // Hiển thị trang chọn chế độ học (Flashcard, Quiz, Write, Match)
    [Route("/Study/{setId}")]
    public async Task<IActionResult> Index(int setId)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        // Kiểm tra bộ thẻ có tồn tại và người dùng có quyền học không
        var set = await _setService.GetAccessibleSetAsync(setId, user.Id);
        if (set == null) return NotFound();

        // Truyền thông tin bộ thẻ qua ViewBag
        ViewBag.SetTitle = set.Title;
        ViewBag.SetId = setId;
        return View();
    }

    // Hiển thị giao diện học flashcard
    // Tham số index: vị trí thẻ hiện tại (mặc định = 0)
    [Route("/Study/{setId}/Flashcard")]
    public async Task<IActionResult> Flashcard(int setId, int index = 0, bool starredOnly = false)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        // Kiểm tra bộ thẻ có tồn tại và người dùng có quyền học không
        var set = await _setService.GetAccessibleSetAsync(setId, user.Id);
        if (set == null) return NotFound();

        // Lấy danh sách thẻ để học
        var cards = await _studyService.GetFlashcardsForStudyAsync(setId, starredOnly);
        if (!cards.Any())
        {
            // Bộ thẻ chưa có thẻ nào → quay lại trang chọn chế độ
            TempData["Message"] = starredOnly
                ? "Bộ thẻ này chưa có thẻ nào được đánh sao."
                : "Bộ thẻ này chưa có thẻ nào.";
            return RedirectToAction("Index", new { setId });
        }

        // Tạo ViewModel cho trang học flashcard
        var model = new FlashcardStudyViewModel
        {
            SetId = setId,
            SetTitle = set.Title,
            Flashcards = cards,
            CurrentIndex = Math.Clamp(index, 0, cards.Count - 1), // Giới hạn index hợp lệ
            StarredOnly = starredOnly
        };

        return View(model);
    }

    // Xử lý đánh dấu thẻ đã biết hoặc chưa biết
    // learned = true: đã biết, learned = false: chưa biết
    [HttpPost]
    [Route("/Study/{setId}/Flashcard/Mark")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkLearned(int setId, int cardId, bool learned)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Unauthorized();
            }
            return Challenge();
        }

        // Lưu tiến trình học vào database
        await _studyService.MarkLearnedAsync(user.Id, cardId, learned);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true });
        }

        // Quay lại trang flashcard hiện tại
        return RedirectToAction("Flashcard", new { setId });
    }

    // Xử lý hoàn thành buổi học
    [HttpPost]
    [Route("/Study/{setId}/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int setId)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Unauthorized();
            }
            return Challenge();
        }

        // Ghi nhận phiên học hoàn thành
        await _studyService.CompleteSessionAsync(user.Id, setId, Models.Entities.StudyMode.Flashcard);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, redirectUrl = Url.Action("Index", new { setId }) });
        }

        // Hiển thị thông báo thành công
        TempData["Success"] = "Hoàn thành buổi học!";
        return RedirectToAction("Index", new { setId });
    }
}
