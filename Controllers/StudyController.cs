using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using ltwnc.Services.Study;
using ltwnc.Models.ViewModels.Flashcards;
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

    // Entry route: lưu bộ lọc nếu có rồi mở thẳng Flashcard.
    [AllowAnonymous]
    [Route("/Study/{setId:int}")]
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

        return RedirectToAction(nameof(Flashcard), new
        {
            setId,
            starredOnly,
            unlearnedOnly
        });
    }

    // GET màn flashcard: gộp filter query + settings; bộ lọc rỗng được tự bỏ.
    [AllowAnonymous]
    [Route("/Study/{setId:int}/Flashcard")]
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
            if (vocabularyCards.Any() && (effectiveStarredOnly || effectiveUnlearnedOnly))
            {
                TempData["Message"] = "Không có thẻ phù hợp. Bộ lọc đã được bỏ để bạn tiếp tục học.";

                if (userId != null)
                {
                    await _studyService.SaveFilterSettingsAsync(userId, false, false);
                }

                return RedirectToAction(nameof(Flashcard), new
                {
                    setId,
                    starredOnly = false,
                    unlearnedOnly = false
                });
            }

            TempData["Message"] = "Thêm ít nhất một thẻ để bắt đầu học.";
            return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
        }

        StudyModeSelectorViewModel modeSelector =
            await _studyService.GetStudyModeSelectorDataAsync(setId, userId);

        StudySession? session = userId == null
            ? null
            : await _studyService.StartSessionAsync(userId, setId, StudyMode.Flashcard);

        FlashcardStudyViewModel model = new FlashcardStudyViewModel
        {
            SetId = setId,
            SessionId = session?.Id ?? 0,
            SetTitle = set.Title,
            Flashcards = FlashcardViewModelMapper.FromEntities(cards),
            VocabularyCards = FlashcardViewModelMapper.FromEntities(vocabularyCards),
            ProgressByCardId = progressByCardId.ToDictionary(
                entry => entry.Key,
                entry => FlashcardProgressViewModel.FromEntity(entry.Value)),
            CurrentIndex = Math.Clamp(index, 0, cards.Count - 1),
            StarredOnly = effectiveStarredOnly,
            Settings = StudySettingsMapper.ToViewModel(settings),
            IsAuthenticated = userId != null,
            UnlearnedOnly = effectiveUnlearnedOnly,
            Modes = modeSelector.Modes
        };

        return View(model);
    }

    // POST đánh dấu đã biết / chưa biết; AJAX -> JSON, form -> redirect Flashcard
    [HttpPost]
    [Route("/Study/{setId:int}/Flashcard/Mark")]
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
    [Route("/Study/{setId:int}/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int setId, int sessionId)
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
            await _studyService.CompleteSessionAsync(userId, setId, sessionId);
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
    [Route("/Study/{setId:int}/Flashcard/{cardId:int}/ToggleStar")]
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

    [HttpGet]
    [AllowAnonymous]
    [Route("/Study/Settings")]
    public IActionResult Settings()
    {
        // GET route này giữ contract routing cũ khi đường dẫn bị gọi nhầm: quay về trang chi tiết set #0.
        return RedirectToAction("Details", "FlashcardSet", new { id = 0 });
    }

    // POST AJAX lưu toàn bộ UserStudySettings
    [HttpPost]
    [Route("/Study/Settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings([FromForm] StudySettingsViewModel settings)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            UserStudySettings saved = await _studyService.SaveSettingsAsync(
                userId,
                StudySettingsMapper.ToEntity(settings));
            return Json(new
            {
                success = true,
                settings = StudySettingsMapper.ToViewModel(saved)
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { success = false, message = "Could not save study settings." });
        }
    }

    // GET tắt StarredOnly + UnlearnedOnly rồi về hub (thoát lọc rỗng)
    [Authorize]
    [HttpPost]
    [Route("/Study/{setId:int}/ClearFilters")]
    [ValidateAntiForgeryToken]
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
                TimingMode = QuizTimingMode.Preset,
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
    [Route("/Study/{setId}/Quiz/Start", Name = "QuizStartPost")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuizStart(int setId, QuizSetupViewModel input)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        bool hasValidTimingSelection = input.TimingMode switch
        {
            QuizTimingMode.Preset => input.SelectedPresetMinutes is 5 or 10 or 15 or 20
                && input.CustomMinutes is null,
            QuizTimingMode.Custom => input.CustomMinutes is >= QuizService.MinimumQuizMinutes
                and <= QuizService.MaximumQuizMinutes
                && input.SelectedPresetMinutes is null,
            QuizTimingMode.Untimed => input.SelectedPresetMinutes is null
                && input.CustomMinutes is null,
            _ => false
        };
        int? timeLimitMinutes = input.TimingMode switch
        {
            QuizTimingMode.Preset => input.SelectedPresetMinutes,
            QuizTimingMode.Custom => input.CustomMinutes,
            QuizTimingMode.Untimed => null,
            _ => null
        };
        if (!hasValidTimingSelection)
        {
            string validationKey = input.TimingMode == QuizTimingMode.Custom
                ? nameof(QuizSetupViewModel.CustomMinutes)
                : nameof(QuizSetupViewModel.TimingMode);
            ModelState.AddModelError(
                validationKey,
                input.TimingMode == QuizTimingMode.Custom
                    ? $"Thời lượng phải từ 1 đến {QuizService.MaximumQuizMinutes} phút."
                    : "Lựa chọn thời gian không hợp lệ.");
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
                timeLimitMinutes);
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
    public async Task<IActionResult> Quiz(int setId, int sessionId, int? questionId = null)
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
                userId,
                questionId);
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
                CurrentNumber = question.OrderIndex + 1,
                TotalQuestions = state.TotalQuestions,
                AnsweredCount = state.AnsweredCount,
                CorrectCount = state.CorrectCount,
                DeadlineUtc = state.DeadlineUtc,
                RemainingSeconds = state.RemainingSeconds,
                Direction = question.Direction,
                PromptText = question.PromptText,
                Choices = question.Choices.ToList(),
                IsReviewOnly = state.IsReviewOnly,
                SelectedChoiceIndex = state.SelectedChoiceIndex,
                CorrectChoiceIndex = state.CorrectChoiceIndex,
                IsCorrect = state.IsCorrect,
                PreviousQuestionId = state.PreviousQuestionId,
                NextQuestionId = state.NextQuestionId,
                CurrentPendingQuestionId = state.CurrentPendingQuestionId
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
        catch (QuizSessionAbandonedException exception)
        {
            return RedirectStaleQuiz(setId, exception);
        }
        catch (QuizExpiredException)
        {
            return RedirectToAction(nameof(QuizResult), new { setId, sessionId });
        }
    }

    [HttpPost]
    [Route("/Study/{setId}/Quiz/{sessionId:int}/Answer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuizAnswer(
        int setId,
        int sessionId,
        int? questionId,
        int? selectedChoiceIndex)
    {
        if (!ModelState.IsValid || questionId is null || selectedChoiceIndex is null)
        {
            return BadRequest();
        }

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
                questionId.Value,
                selectedChoiceIndex.Value,
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
        catch (QuizSessionAbandonedException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                stale = true,
                nextUrl = GetStaleQuizUrl(setId, exception)
            });
        }
        catch (QuizExpiredException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                expired = true,
                message = exception.Message,
                nextUrl = Url.Action(nameof(QuizResult), new { setId, sessionId })
            });
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
    [Route("/Study/{setId}/Quiz/{sessionId:int}/Timeout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuizTimeout(int setId, int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            await _quizService.CompleteExpiredAsync(setId, sessionId, userId);
            string? resultUrl = Url.Action(nameof(QuizResult), new { setId, sessionId });
            if (IsAjaxRequest())
            {
                return Json(new { success = true, nextUrl = resultUrl });
            }

            return RedirectToAction(nameof(QuizResult), new { setId, sessionId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (QuizSessionAbandonedException exception)
        {
            if (IsAjaxRequest())
            {
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    success = false,
                    stale = true,
                    nextUrl = GetStaleQuizUrl(setId, exception)
                });
            }

            return RedirectStaleQuiz(setId, exception);
        }
        catch (QuizNotExpiredException exception)
        {
            string? nextUrl = Url.Action(nameof(Quiz), new { setId, sessionId });
            if (IsAjaxRequest())
            {
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    success = false,
                    expired = false,
                    remainingSeconds = exception.RemainingSeconds,
                    nextUrl
                });
            }

            return RedirectToAction(nameof(Quiz), new { setId, sessionId });
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
            TempData["Message"] = exception.Message;
            return RedirectToAction(nameof(Quiz), new { setId, sessionId });
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
    [Route("/Study/{setId}/Quiz/{sessionId:int}/Restart")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuizRestart(int setId, int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            StudySession session = await _quizService.RestartAsync(setId, sessionId, userId);
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
        catch (QuizExpiredException)
        {
            return RedirectToAction(nameof(QuizResult), new { setId, sessionId });
        }
        catch (QuizSessionAbandonedException exception)
        {
            return RedirectStaleQuiz(setId, exception);
        }
        catch (QuizUnavailableException exception)
        {
            TempData["Message"] = exception.Message;
            return RedirectToAction(nameof(Quiz), new { setId, sessionId });
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
    [Route("/Study/{setId:int}/Dictation")]
    public async Task<IActionResult> Dictation(int setId, int? retrySessionId = null)
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
        DictationContentMode contentMode = settings.DictationContentMode;
        List<Flashcard> cards;

        if (retrySessionId.HasValue)
        {
            try
            {
                DictationRetryPlan retryPlan = await _dictationService.GetRetryPlanAsync(
                    retrySessionId.Value,
                    setId,
                    userId);
                cards = retryPlan.Cards;
                contentMode = retryPlan.ContentMode;
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException exception)
            {
                TempData["Message"] = exception.Message;
                return RedirectToAction(nameof(Index), new { setId });
            }
        }
        else
        {
            cards = await _dictationService.GetCardsForDictationAsync(
                setId,
                userId,
                settings);
        }

        if (!cards.Any())
        {
            if (retrySessionId.HasValue)
            {
                TempData["Message"] = "Không còn thẻ sai khả dụng để ôn lại.";
                return RedirectToAction(
                    nameof(DictationResult),
                    new { setId, sessionId = retrySessionId.Value });
            }

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
            contentMode,
            cards.Count,
            cards);

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

        StudySettingsViewModel settingsViewModel = StudySettingsMapper.ToViewModel(settings);
        settingsViewModel.DictationContentMode = session.DictationContentMode;

        DictationStudyViewModel viewModel = new DictationStudyViewModel
        {
            SetId = setId,
            SetTitle = set.Title,
            SessionId = session.Id,
            Settings = settingsViewModel,
            ContentMode = session.DictationContentMode,
            Cards = cardViewModels
        };

        return View(viewModel);
    }

    // POST AJAX chấm một câu; trả isCorrect, hint, wordComparison...
    [HttpPost]
    [Route("/Study/{setId:int}/Dictation/Check")]
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
                setId,
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
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                message = exception.Message
            });
        }
    }

    // POST AJAX đóng phiên + điểm; JSON redirectUrl màn result
    [HttpPost]
    [Route("/Study/{setId:int}/Dictation/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DictationComplete(int setId, int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            await _dictationService.CompleteSessionAsync(sessionId, setId, userId);
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
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                message = exception.Message
            });
        }
    }

    // GET tổng kết phiên nghe chép (owner set + owner session)
    [Authorize]
    [Route("/Study/{setId:int}/Dictation/Result/{sessionId:int}")]
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
            DictationResult result = await _dictationService.GetSessionResultAsync(
                sessionId,
                setId,
                userId);

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
        catch (InvalidOperationException exception)
        {
            TempData["Message"] = exception.Message;
            return RedirectToAction(nameof(Dictation), new { setId });
        }
    }

    [Authorize]
    [Route("/Study/{setId:int}/History")]
    public async Task<IActionResult> DictationHistory(int setId)
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
            List<DictationHistoryItem> history = await _dictationService.GetHistoryAsync(
                setId,
                userId);
            return View(new DictationHistoryViewModel
            {
                SetId = setId,
                SetTitle = set.Title,
                Items = history.Select(item => new DictationHistoryItemViewModel
                {
                    SessionId = item.SessionId,
                    PromptText = item.PromptText,
                    AnsweredText = item.AnsweredText,
                    CorrectAnswer = item.CorrectAnswer,
                    Definition = item.Definition,
                    AnsweredAt = item.AnsweredAt
                }).ToList()
            });
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

    private IActionResult RedirectStaleQuiz(
        int setId,
        QuizSessionAbandonedException exception)
    {
        return exception.ActiveSessionId is int activeSessionId
            ? RedirectToAction(nameof(Quiz), new { setId, sessionId = activeSessionId })
            : RedirectToAction(nameof(QuizStart), new { setId });
    }

    private string? GetStaleQuizUrl(
        int setId,
        QuizSessionAbandonedException exception)
    {
        return exception.ActiveSessionId is int activeSessionId
            ? Url.Action(nameof(Quiz), new { setId, sessionId = activeSessionId })
            : Url.Action(nameof(QuizStart), new { setId });
    }
}
