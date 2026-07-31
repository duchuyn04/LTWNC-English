using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using ltwnc.Services.Auth;
using ltwnc.Services.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

[Authorize]
public sealed class ReviewController : Controller
{
    private readonly IReviewService _reviewService;
    private readonly IReviewSettingsService? _reviewSettingsService;
    private readonly ICurrentUser _currentUser;

    public ReviewController(
        IReviewService reviewService,
        ICurrentUser currentUser,
        IReviewSettingsService? reviewSettingsService = null)
    {
        _reviewService = reviewService;
        _currentUser = currentUser;
        _reviewSettingsService = reviewSettingsService;
    }

    [HttpGet]
    [Route("/Review")]
    public async Task<IActionResult> Index()
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        ReviewSessionViewModel? active = await _reviewService.GetActiveSessionAsync(userId);
        return active == null
            ? View()
            : RedirectToAction(nameof(Session), new { sessionId = active.SessionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/Review/Start")]
    public async Task<IActionResult> Start()
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        ReviewSessionViewModel? session = await _reviewService.StartAsync(userId);
        if (session == null)
        {
            TempData["Message"] = "Chưa có thẻ mới phù hợp để bắt đầu ôn tập.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Session), new { sessionId = session.SessionId });
    }

    [HttpGet]
    [Route("/Review/Set/{setId:int}/Settings")]
    public async Task<IActionResult> Settings(int setId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        if (_reviewSettingsService == null)
        {
            return NotFound();
        }

        ReviewSettingsViewModel? settings = await _reviewSettingsService
            .GetOrCreateAsync(userId, setId);
        return settings == null ? NotFound() : Ok(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/Review/Set/{setId:int}/Settings")]
    public async Task<IActionResult> SaveSettings(
        int setId,
        ReviewSettingsViewModel input)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (_reviewSettingsService == null)
        {
            return NotFound();
        }

        ReviewSettingsViewModel? saved = await _reviewSettingsService
            .SaveAsync(userId, setId, input);
        return saved == null ? NotFound() : Ok(saved);
    }

    [HttpGet]
    [Route("/Review/{sessionId:int}")]
    public async Task<IActionResult> Session(int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        ReviewSessionViewModel? session = await _reviewService.GetSessionAsync(sessionId, userId);
        if (session == null)
        {
            return NotFound();
        }

        return session.IsFinished
            ? RedirectToAction(nameof(Result), new { sessionId })
            : View(session);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/Review/{sessionId:int}/Rate")]
    public async Task<IActionResult> Rate(
        int sessionId,
        int flashcardId,
        ReviewRating rating,
        bool answerRevealed)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            ReviewRatingResult result = await _reviewService.RateAsync(
                userId,
                sessionId,
                flashcardId,
                rating,
                answerRevealed);
            return result.Session.IsFinished
                ? RedirectToAction(nameof(Result), new { sessionId = result.Session.SessionId })
                : RedirectToAction(nameof(Session), new { sessionId = result.Session.SessionId });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new
            {
                success = false,
                message = exception.Message
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/Review/{sessionId:int}/End")]
    public async Task<IActionResult> End(int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Unauthorized();
        }

        ReviewSessionViewModel? session = await _reviewService.EndAsync(userId, sessionId);
        return session == null
            ? NotFound()
            : RedirectToAction(nameof(Result), new { sessionId = session.SessionId });
    }

    [HttpGet]
    [Route("/Review/{sessionId:int}/Result")]
    public async Task<IActionResult> Result(int sessionId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        ReviewSessionViewModel? session = await _reviewService.GetSessionAsync(sessionId, userId);
        if (session == null)
        {
            return NotFound();
        }

        return session.IsFinished
            ? View(session)
            : RedirectToAction(nameof(Session), new { sessionId });
    }
}
