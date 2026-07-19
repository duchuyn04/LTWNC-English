using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using ltwnc.Services.Study;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Models.Entities;

namespace ltwnc.Controllers;

// Học: Study Hub, Flashcard, Dictation, settings. Class [Authorize]; vài GET AllowAnonymous.
[Authorize]
public class StudyController : Controller
{
    // Settings, progress, hub, mark learned, complete flashcard session
    private readonly IStudyService _studyService;

    // Lấy thẻ / chấm / complete / result dictation
    private readonly IDictationService _dictationService;

    // Tạo phiên / lấy câu hỏi / chấm / kết quả quiz
    private readonly IQuizService _quizService;

    // Kiểm tra owner set, toggle star
    private readonly IFlashcardSetService _setService;

    // User hiện tại từ cookie claims
    private readonly ICurrentUser _currentUser;

    public StudyController(
        IStudyService studyService,
        IDictationService dictationService,
        IQuizService quizService,
        IFlashcardSetService setService,
        ICurrentUser currentUser)
    {
        _studyService = studyService;
        _dictationService = dictationService;
        _quizService = quizService;
        _setService = setService;
        _currentUser = currentUser;
    }

    // GET Study Hub: chỉ set của owner; query filter ghi settings nếu đã login
    [AllowAnonymous]
    [Route("/Study/{setId}")]
    public async Task<IActionResult> Index(
        int setId,
        bool? starredOnly = null,
        bool? unlearnedOnly = null)
    {
        string? userId = _currentUser.UserId;

        FlashcardSet? set = await _setService.GetOwnedSetAsync(setId, userId!);
        if (set == null)
        {
            return RedirectToAction("Details", "FlashcardSet", new { id = setId });
        }

        if (userId != null && (starredOnly.HasValue || unlearnedOnly.HasValue))
        {
            await _studyService.SaveFilterSettingsAsync(userId, starredOnly, unlearnedOnly);
        }

        StudyModeSelectorViewModel model =
            await _studyService.GetStudyModeSelectorDataAsync(setId, userId);
        return View(model);
    }

    // GET màn flashcard: gộp filter query + settings; empty -> hub + TempData
    [AllowAnonymous]
    [Route("/Study/{setId}/Flashcard")]
    public async Task<IActionResult> Flashcard(
        int setId,
        int index = 0,
        bool? starredOnly = null,
        bool? unlearnedOnly = null)
    {
        string? userId = _currentUser.UserId;

        UserStudySettings settings = await _studyService.GetSettingsAsync(userId);

        // Query string thắng settings đã lưu (JS initialSettings cũng dùng bộ này)
        bool effectiveStarredOnly = starredOnly ?? settings.StarredOnly;
        bool effectiveUnlearnedOnly = unlearnedOnly ?? settings.UnlearnedOnly;
        settings.StarredOnly = effectiveStarredOnly;
        settings.UnlearnedOnly = effectiveUnlearnedOnly;

        FlashcardSet? set = await _setService.GetOwnedSetAsync(setId, userId!);
        if (set == null)
        {
            return RedirectToAction("Details", "FlashcardSet", new { id = setId });
        }

        List<Flashcard> cards = await _studyService.GetCardsForModeAsync(
            StudyMode.Flashcard,
            setId,
            settings,
            userId);

        // List đầy đủ (không filter) cho UI phụ
        UserStudySettings vocabularySettings = new UserStudySettings
        {
            StarredOnly = false,
            UnlearnedOnly = false
        };
        List<Flashcard> vocabularyCards = await _studyService.GetCardsForModeAsync(
            StudyMode.Flashcard,
            setId,
            vocabularySettings,
            userId);

        Dictionary<int, UserProgress> progressByCardId =
            await _studyService.GetProgressByCardIdAsync(setId, userId);

        if (!cards.Any())
        {
            if (effectiveStarredOnly || effectiveUnlearnedOnly)
            {
                TempData["Message"] = "Không có thẻ phù hợp với bộ lọc hiện tại.";
            }
            else
            {
                TempData["Message"] = "Bộ thẻ này chưa có thẻ nào.";
            }

            return RedirectToAction("Index", new { setId });
        }

        FlashcardStudyViewModel model = new FlashcardStudyViewModel
        {
            SetId = setId,
            SetTitle = set.Title,
            Flashcards = cards,
            VocabularyCards = vocabularyCards,
            ProgressByCardId = progressByCardId,
            CurrentIndex = Math.Clamp(index, 0, cards.Count - 1),
            StarredOnly = effectiveStarredOnly,
            Settings = settings,
            IsAuthenticated = userId != null,
            UnlearnedOnly = effectiveUnlearnedOnly
        };

        return View(model);
    }

    // POST đánh dấu đã biết / chưa biết; AJAX -> JSON, form -> redirect Flashcard
    [HttpPost]
    [Route("/Study/{setId}/Flashcard/Mark")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkLearned(int setId, int cardId, bool learned)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            if (IsAjaxRequest())
            {
                return Unauthorized();
            }

            return Challenge();
        }

        try
        {
            await _studyService.MarkLearnedAsync(userId, setId, cardId, learned);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (IsAjaxRequest())
        {
            return Json(new { success = true });
        }

        return RedirectToAction("Flashcard", new { setId });
    }

    // POST hoàn thành buổi Flashcard; AJAX kèm redirectUrl hub
    [HttpPost]
    [Route("/Study/{setId}/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int setId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            if (IsAjaxRequest())
            {
                return Unauthorized();
            }

            return Challenge();
        }

        try
        {
            await _studyService.CompleteSessionAsync(userId, setId, StudyMode.Flashcard);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                redirectUrl = Url.Action("Index", new { setId })
            });
        }

        TempData["Success"] = "Hoàn thành buổi học!";
        return RedirectToAction("Index", new { setId });
    }

    // POST AJAX toggle sao thẻ (owner)
    [HttpPost]
    [Route("/Study/{setId}/Flashcard/{cardId}/ToggleStar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStar(int setId, int cardId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            bool isStarred = await _setService.ToggleStarAsync(cardId, userId);
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

    // POST AJAX lưu toàn bộ UserStudySettings
    [HttpPost]
    [Route("/Study/Settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings([FromForm] UserStudySettings settings)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            UserStudySettings saved = await _studyService.SaveSettingsAsync(userId, settings);
            return Json(new { success = true, settings = saved });
        }
        catch (Exception)
        {
            return StatusCode(500, new { success = false, message = "Could not save study settings." });
        }
    }

    // GET tắt StarredOnly + UnlearnedOnly rồi về hub (thoát lọc rỗng)
    [Authorize]
    [Route("/Study/{setId}/ClearFilters")]
    public async Task<IActionResult> ClearFilters(int setId)
    {
        string? userId = _currentUser.UserId;
        if (userId != null)
        {
            UserStudySettings settings = await _studyService.GetSettingsAsync(userId);
            settings.StarredOnly = false;
            settings.UnlearnedOnly = false;
            await _studyService.SaveSettingsAsync(userId, settings);
        }

        return RedirectToAction("Index", new { setId });
    }

    [HttpGet]
    [Route("/Study/{setId}/Quiz")]
    public async Task<IActionResult> QuizStart(int setId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            QuizSetupState state = await _quizService.GetSetupAsync(setId, userId);
            return View("QuizSetup", new QuizSetupViewModel
            {
                SetId = state.SetId,
                SetTitle = state.SetTitle,
                SelectedPresetMinutes = QuizService.DefaultQuizMinutes,
                ActiveSessionId = state.ActiveSession?.Id
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

    [HttpPost]
    [Route("/Study/{setId}/Quiz/Start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuizStart(int setId, QuizSetupViewModel input)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        int? timeLimitMinutes = input.CustomMinutes ?? input.SelectedPresetMinutes;
        bool isPreset = input.SelectedPresetMinutes is 5 or 10 or 15 or 20;
        bool isCustom = input.CustomMinutes is >= QuizService.MinimumQuizMinutes
            and <= QuizService.MaximumQuizMinutes;
        if (timeLimitMinutes == null || (input.CustomMinutes.HasValue ? !isCustom : !isPreset))
        {
            ModelState.AddModelError(
                nameof(QuizSetupViewModel.CustomMinutes),
                $"Thời lượng phải từ 1 đến {QuizService.MaximumQuizMinutes} phút.");
        }

        if (!ModelState.IsValid)
        {
            return await RenderQuizSetupAsync(setId, userId, input);
        }

        try
        {
            UserStudySettings settings = await _studyService.GetSettingsAsync(userId);
            StudySession session = await _quizService.StartNewAsync(
                setId,
                userId,
                settings,
                timeLimitMinutes!.Value);
            return RedirectToAction(nameof(Quiz), new { setId, sessionId = session.Id });
        }
        catch (QuizUnavailableException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await RenderQuizSetupAsync(setId, userId, input);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(
                nameof(QuizSetupViewModel.CustomMinutes),
                $"Thời lượng phải từ 1 đến {QuizService.MaximumQuizMinutes} phút.");
            return await RenderQuizSetupAsync(setId, userId, input);
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

    private async Task<IActionResult> RenderQuizSetupAsync(
        int setId,
        string userId,
        QuizSetupViewModel input)
    {
        try
        {
            QuizSetupState state = await _quizService.GetSetupAsync(setId, userId);
            input.SetId = state.SetId;
            input.SetTitle = state.SetTitle;
            input.ActiveSessionId = state.ActiveSession?.Id;
            return View("QuizSetup", input);
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

    [HttpGet]
    [Route("/Study/{setId}/Quiz/{sessionId:int}")]
    public async Task<IActionResult> Quiz(int setId, int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            QuizQuestionState state = await _quizService.GetCurrentQuestionAsync(
                setId,
                sessionId,
                userId);
            if (state.IsComplete)
            {
                return RedirectToAction(nameof(QuizResult), new { setId, sessionId });
            }

            QuizSessionQuestion question = state.Question!;
            QuizStudyViewModel model = new()
            {
                SetId = state.SetId,
                SetTitle = state.SetTitle,
                SessionId = state.SessionId,
                QuestionId = question.Id,
                CurrentNumber = state.AnsweredCount + 1,
                TotalQuestions = state.TotalQuestions,
                CorrectCount = state.CorrectCount,
                Direction = question.Direction,
                PromptText = question.PromptText,
                Choices = question.Choices.ToList()
            };

            return View(model);
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest();
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

    [HttpPost]
    [Route("/Study/{setId}/Quiz/{sessionId:int}/Answer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuizAnswer(
        int setId,
        int sessionId,
        int questionId,
        int selectedChoiceIndex)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            QuizAnswerResult result = await _quizService.AnswerAsync(
                setId,
                sessionId,
                questionId,
                selectedChoiceIndex,
                userId);
            string? nextUrl = result.IsLastQuestion
                ? Url.Action(nameof(QuizResult), new { setId, sessionId })
                : Url.Action(nameof(Quiz), new { setId, sessionId });

            return Json(new
            {
                success = true,
                isCorrect = result.IsCorrect,
                correctChoiceIndex = result.CorrectChoiceIndex,
                isLastQuestion = result.IsLastQuestion,
                nextUrl
            });
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (QuizConflictException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                message = exception.Message
            });
        }
    }

    [HttpGet]
    [Route("/Study/{setId}/Quiz/Result/{sessionId:int}")]
    public async Task<IActionResult> QuizResult(int setId, int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            QuizSessionResult result = await _quizService.GetResultAsync(setId, sessionId, userId);
            QuizResultViewModel model = new()
            {
                SetId = result.SetId,
                SetTitle = result.SetTitle,
                SessionId = result.SessionId,
                Score = result.Score,
                TotalQuestions = result.TotalQuestions,
                CorrectCount = result.CorrectCount,
                WrongAnswers = result.WrongAnswers.Select(answer => new QuizWrongAnswerViewModel
                {
                    Direction = answer.Direction,
                    PromptText = answer.PromptText,
                    SelectedAnswer = answer.SelectedAnswer,
                    CorrectAnswer = answer.CorrectAnswer
                }).ToList()
            };

            return View(model);
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (QuizConflictException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                message = exception.Message
            });
        }
    }

    [HttpPost]
    [Route("/Study/{setId}/Quiz/{sessionId:int}/RetryWrong")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryWrong(int setId, int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            StudySession session = await _quizService.RetryWrongAsync(setId, sessionId, userId);
            return RedirectToAction(nameof(Quiz), new { setId, sessionId = session.Id });
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (QuizUnavailableException exception)
        {
            TempData["Message"] = exception.Message;
            return RedirectToAction(nameof(QuizResult), new { setId, sessionId });
        }
        catch (QuizConflictException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                message = exception.Message
            });
        }
    }

    [HttpPost]
    [Route("/Study/{setId}/Quiz/{sessionId:int}/RetryAll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryAll(int setId, int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            StudySession session = await _quizService.RetryAllAsync(setId, sessionId, userId);
            return RedirectToAction(nameof(Quiz), new { setId, sessionId = session.Id });
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (QuizUnavailableException exception)
        {
            TempData["Message"] = exception.Message;
            return RedirectToAction(nameof(QuizResult), new { setId, sessionId });
        }
        catch (QuizConflictException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                message = exception.Message
            });
        }
    }

    // GET màn Dictation: tạo session, map thẻ -> view model; empty -> hub
    [Authorize]
    [Route("/Study/{setId}/Dictation")]
    public async Task<IActionResult> Dictation(int setId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        FlashcardSet? set = await _setService.GetOwnedSetAsync(setId, userId);
        if (set == null)
        {
            return RedirectToAction("Details", "FlashcardSet", new { id = setId });
        }

        UserStudySettings settings = await _studyService.GetSettingsAsync(userId);
        List<Flashcard> cards = await _dictationService.GetCardsForDictationAsync(
            setId,
            userId,
            settings);

        if (!cards.Any())
        {
            string message;

            bool exampleMode = settings.DictationContentMode == DictationContentMode.ExampleSentence;
            bool anyExampleInSet = exampleMode
                && await _dictationService.AnyCardHasExampleSentenceAsync(setId);

            if (exampleMode && !anyExampleInSet)
            {
                message = "Bộ thẻ chưa có câu ví dụ để nghe chép.";
            }
            else if (settings.StarredOnly || settings.UnlearnedOnly)
            {
                message = "Không có thẻ phù hợp với bộ lọc hiện tại.";
            }
            else
            {
                message = "Bộ thẻ này chưa có thẻ nào.";
            }

            TempData["Message"] = message;
            return RedirectToAction("Index", new { setId });
        }

        StudySession session = await _dictationService.CreateSessionAsync(
            userId,
            setId,
            settings.DictationContentMode);

        List<DictationCardViewModel> cardViewModels = new List<DictationCardViewModel>();
        foreach (Flashcard card in cards)
        {
            // Prompt: câu ví dụ hoặc term tùy content mode
            string promptText;
            if (session.DictationContentMode == DictationContentMode.ExampleSentence)
            {
                promptText = card.ExampleSentence;
            }
            else
            {
                promptText = card.FrontText;
            }

            // Ưu tiên ảnh upload nội bộ, không thì URL ngoài
            string? imageUrl = card.ImageUrl;
            if (!string.IsNullOrWhiteSpace(card.UploadedImagePath))
            {
                imageUrl = card.UploadedImagePath;
            }

            cardViewModels.Add(new DictationCardViewModel
            {
                Id = card.Id,
                Term = card.FrontText,
                Definition = card.BackText,
                ExampleSentence = card.ExampleSentence,
                ExampleMeaning = card.ExampleMeaning,
                PromptText = promptText,
                Pronunciation = card.Pronunciation,
                ImageUrl = imageUrl
            });
        }

        DictationStudyViewModel viewModel = new DictationStudyViewModel
        {
            SetId = setId,
            SetTitle = set.Title,
            SessionId = session.Id,
            Settings = settings,
            ContentMode = session.DictationContentMode,
            Cards = cardViewModels
        };

        return View(viewModel);
    }

    // POST AJAX chấm một câu; trả isCorrect, hint, wordComparison...
    [HttpPost]
    [Route("/Study/{setId}/Dictation/Check")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DictationCheck(
        int setId,
        int sessionId,
        int cardId,
        string answeredText)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            UserStudySettings settings = await _studyService.GetSettingsAsync(userId);
            DictationCheckResult result = await _dictationService.CheckAnswerAsync(
                sessionId,
                cardId,
                answeredText,
                userId,
                settings.DictationAcceptSynonyms);

            // Serialize word comparison cho JS highlight
            var wordComparison = result.WordComparison.Select(word => new
            {
                status = word.Status.ToString(),
                answeredWord = word.AnsweredWord,
                correctWord = word.CorrectWord
            });

            return Json(new
            {
                success = true,
                isCorrect = result.IsCorrect,
                correctAnswer = result.CorrectAnswer,
                hint = result.Hint,
                exampleMeaning = result.ExampleMeaning,
                wordComparison = wordComparison
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

    // POST AJAX đóng phiên + điểm; JSON redirectUrl màn result
    [HttpPost]
    [Route("/Study/{setId}/Dictation/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DictationComplete(int setId, int sessionId, int score)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

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

    // GET tổng kết phiên nghe chép (owner set + owner session)
    [Authorize]
    [Route("/Study/{setId}/Dictation/Result/{sessionId}")]
    public async Task<IActionResult> DictationResult(int setId, int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        FlashcardSet? set = await _setService.GetOwnedSetAsync(setId, userId);
        if (set == null)
        {
            return RedirectToAction("Details", "FlashcardSet", new { id = setId });
        }

        try
        {
            DictationResult result = await _dictationService.GetSessionResultAsync(sessionId, userId);

            List<DictationResultCardViewModel> wrongCards = new List<DictationResultCardViewModel>();
            foreach (DictationResultCard card in result.WrongCards)
            {
                wrongCards.Add(new DictationResultCardViewModel
                {
                    Id = card.Id,
                    Term = card.Term,
                    Definition = card.Definition,
                    Pronunciation = card.Pronunciation,
                    ExampleSentence = card.ExampleSentence,
                    ExampleMeaning = card.ExampleMeaning
                });
            }

            DictationResultViewModel viewModel = new DictationResultViewModel
            {
                SetId = setId,
                SetTitle = set.Title,
                SessionId = sessionId,
                ContentMode = result.ContentMode,
                TotalCards = result.TotalCards,
                CorrectCount = result.CorrectCount,
                Score = result.Score,
                WrongCards = wrongCards
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

    // Header X-Requested-With = XMLHttpRequest (fetch/jQuery)
    private bool IsAjaxRequest()
    {
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
    }
}
