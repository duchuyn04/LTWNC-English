using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using ltwnc.Services;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Models.Entities;

namespace ltwnc.Controllers;

// Controller xử lý chức năng học flashcard — yêu cầu đăng nhập
[Authorize]
public class StudyController : Controller
{
    private readonly StudyService _studyService;
    private readonly DictationService _dictationService;
    private readonly FlashcardSetService _setService;
    private readonly UserManager<IdentityUser> _userManager;

    // Inject các service: học tập, nghe chép, bộ thẻ, UserManager
    public StudyController(
        StudyService studyService,
        DictationService dictationService,
        FlashcardSetService setService,
        UserManager<IdentityUser> userManager)
    {
        _studyService = studyService;
        _dictationService = dictationService;
        _setService = setService;
        _userManager = userManager;
    }

    // Hiển thị trang chọn chế độ học (Flashcard, Quiz, Write, Match)
    [AllowAnonymous]
    [Route("/Study/{setId}")]
    public async Task<IActionResult> Index(int setId)
    {
        var user = await _userManager.GetUserAsync(User);

        // Kiểm tra bộ thẻ có tồn tại và người dùng có quyền học không
        var set = await _setService.GetAccessibleSetAsync(setId, user?.Id);
        if (set == null) return NotFound();

        // Truyền thông tin bộ thẻ qua ViewBag
        ViewBag.SetTitle = set.Title;
        ViewBag.SetId = setId;
        return View();
    }

    // Hiển thị giao diện học flashcard
    // Tham số index: vị trí thẻ hiện tại (mặc định = 0)
    [AllowAnonymous]
    [Route("/Study/{setId}/Flashcard")]
    public async Task<IActionResult> Flashcard(int setId, int index = 0, bool? starredOnly = null, bool? unlearnedOnly = null)
    {
        var user = await _userManager.GetUserAsync(User);

        // Đọc settings và kết hợp bộ lọc
        var settings = await _studyService.GetSettingsAsync(user?.Id);
        var effectiveStarredOnly = starredOnly ?? settings.StarredOnly;
        var effectiveUnlearnedOnly = unlearnedOnly ?? settings.UnlearnedOnly;
        settings.StarredOnly = effectiveStarredOnly; // ponytail: sync so JS initialSettings matches effective filter
        settings.UnlearnedOnly = effectiveUnlearnedOnly;

        // Kiểm tra bộ thẻ có tồn tại và người dùng có quyền học không
        var set = await _setService.GetAccessibleSetAsync(setId, user?.Id);
        if (set == null) return NotFound();

        // Lấy danh sách thẻ để học
        var cards = await _studyService.GetFlashcardsForStudyAsync(setId, effectiveStarredOnly, effectiveUnlearnedOnly, user?.Id);
        
        var vocabularyCards = await _studyService.GetFlashcardsForStudyAsync(setId, false, false, user?.Id);
        var progressByCardId = await _studyService.GetProgressByCardIdAsync(setId, user?.Id);

        if (!cards.Any())
        {
            // Bộ thẻ chưa có thẻ nào → quay lại trang chọn chế độ
            TempData["Message"] = effectiveStarredOnly || effectiveUnlearnedOnly
                ? "Không có thẻ phù hợp với bộ lọc hiện tại."
                : "Bộ thẻ này chưa có thẻ nào.";
            return RedirectToAction("Index", new { setId });
        }

        // Tạo ViewModel cho trang học flashcard
        var model = new FlashcardStudyViewModel
        {
            SetId = setId,
            SetTitle = set.Title,
            Flashcards = cards,
            VocabularyCards = vocabularyCards,
            ProgressByCardId = progressByCardId,
            CurrentIndex = Math.Clamp(index, 0, cards.Count - 1), // Giới hạn index hợp lệ
            StarredOnly = effectiveStarredOnly,
            Settings = settings,
            IsAuthenticated = user != null,
            UnlearnedOnly = effectiveUnlearnedOnly
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
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Unauthorized();
            }
            return Challenge();
        }

        try
        {
            // Lưu tiến trình học vào database
            await _studyService.MarkLearnedAsync(user.Id, setId, cardId, learned);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

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
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Unauthorized();
            }
            return Challenge();
        }

        try
        {
            // Ghi nhận phiên học hoàn thành
            await _studyService.CompleteSessionAsync(user.Id, setId, Models.Entities.StudyMode.Flashcard);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, redirectUrl = Url.Action("Index", new { setId }) });
        }

        // Hiển thị thông báo thành công
        TempData["Success"] = "Hoàn thành buổi học!";
        return RedirectToAction("Index", new { setId });
    }

    // Xử lý đánh dấu sao thẻ bằng AJAX
    [HttpPost]
    [Route("/Study/{setId}/Flashcard/{cardId}/ToggleStar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStar(int setId, int cardId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        try
        {
            var isStarred = await _setService.ToggleStarAsync(cardId, user.Id);
            return Json(new { success = true, isStarred = isStarred });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception)
        {
            return BadRequest();
        }
    }

    [HttpPost]
    [Route("/Study/Settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings([FromForm] UserStudySettings settings)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        try
        {
            var saved = await _studyService.SaveSettingsAsync(user.Id, settings);
            return Json(new { success = true, settings = saved });
        }
        catch (Exception)
        {
            return StatusCode(500, new { success = false, message = "Could not save study settings." });
        }
    }

    // Xóa bộ lọc StarredOnly/UnlearnedOnly để thoát khỏi trạng thái lọc rỗng
    [Authorize]
    [Route("/Study/{setId}/ClearFilters")]
    public async Task<IActionResult> ClearFilters(int setId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            var settings = await _studyService.GetSettingsAsync(user.Id);
            settings.StarredOnly = false;
            settings.UnlearnedOnly = false;
            await _studyService.SaveSettingsAsync(user.Id, settings);
        }

        return RedirectToAction("Index", new { setId });
    }

    // Hiển thị giao diện học nghe chép
    // Yêu cầu đăng nhập
    [Authorize]
    [Route("/Study/{setId}/Dictation")]
    public async Task<IActionResult> Dictation(int setId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var set = await _setService.GetAccessibleSetAsync(setId, user.Id);
        if (set == null) return NotFound();

        var settings = await _studyService.GetSettingsAsync(user.Id);
        var cards = await _dictationService.GetCardsForDictationAsync(setId, user.Id, settings);

        if (!cards.Any())
        {
            string message;
            if (settings.DictationContentMode == DictationContentMode.ExampleSentence &&
                !await _dictationService.AnyCardHasExampleSentenceAsync(setId))
            {
                message = "Bộ thẻ chưa có câu ví dụ để nghe chép.";
            }
            else
            {
                message = settings.StarredOnly || settings.UnlearnedOnly
                    ? "Không có thẻ phù hợp với bộ lọc hiện tại."
                    : "Bộ thẻ này chưa có thẻ nào.";
            }
            TempData["Message"] = message;
            return RedirectToAction("Index", new { setId });
        }

        var session = await _dictationService.CreateSessionAsync(user.Id, setId, settings.DictationContentMode);

        var viewModel = new DictationStudyViewModel
        {
            SetId = setId,
            SetTitle = set.Title,
            SessionId = session.Id,
            Settings = settings,
            ContentMode = session.DictationContentMode,
            Cards = cards.Select(c => new DictationCardViewModel
            {
                Id = c.Id,
                Term = c.FrontText,
                Definition = c.BackText,
                ExampleSentence = c.ExampleSentence,
                ExampleMeaning = c.ExampleMeaning,
                PromptText = session.DictationContentMode == DictationContentMode.ExampleSentence
                    ? c.ExampleSentence
                    : c.FrontText,
                Pronunciation = c.Pronunciation,
                ImageUrl = !string.IsNullOrWhiteSpace(c.UploadedImagePath) ? c.UploadedImagePath : c.ImageUrl
            }).ToList()
        };

        return View(viewModel);
    }

    // Kiểm tra đáp án nghe chép qua AJAX
    [HttpPost]
    [Route("/Study/{setId}/Dictation/Check")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DictationCheck(int setId, int sessionId, int cardId, string answeredText)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        try
        {
            var settings = await _studyService.GetSettingsAsync(user.Id);
            var result = await _dictationService.CheckAnswerAsync(
                sessionId, cardId, answeredText, user.Id,
                settings.DictationAnswerMode,
                settings.DictationAcceptSynonyms);

            return Json(new
            {
                success = true,
                isCorrect = result.IsCorrect,
                correctAnswer = result.CorrectAnswer,
                hint = result.Hint,
                exampleMeaning = result.ExampleMeaning,
                wordComparison = result.WordComparison.Select(word => new
                {
                    status = word.Status.ToString(),
                    answeredWord = word.AnsweredWord,
                    correctWord = word.CorrectWord
                })
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // Hoàn thành phiên nghe chép
    [HttpPost]
    [Route("/Study/{setId}/Dictation/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DictationComplete(int setId, int sessionId, int score)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        try
        {
            await _dictationService.CompleteSessionAsync(sessionId, score);
            return Json(new
            {
                success = true,
                redirectUrl = Url.Action("DictationResult", new { setId, sessionId })!
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // Hiển thị màn hình tổng kết phiên nghe chép
    [Authorize]
    [Route("/Study/{setId}/Dictation/Result/{sessionId}")]
    public async Task<IActionResult> DictationResult(int setId, int sessionId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var set = await _setService.GetAccessibleSetAsync(setId, user.Id);
        if (set == null) return NotFound();

        try
        {
            var result = await _dictationService.GetSessionResultAsync(sessionId, user.Id);
            var viewModel = new DictationResultViewModel
            {
                SetId = setId,
                SetTitle = set.Title,
                SessionId = sessionId,
                ContentMode = result.ContentMode,
                TotalCards = result.TotalCards,
                CorrectCount = result.CorrectCount,
                Score = result.Score,
                WrongCards = result.WrongCards.Select(c => new DictationResultCardViewModel
                {
                    Id = c.Id,
                    Term = c.Term,
                    Definition = c.Definition,
                    Pronunciation = c.Pronunciation,
                    ExampleSentence = c.ExampleSentence,
                    ExampleMeaning = c.ExampleMeaning
                }).ToList()
            };

            return View(viewModel);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
