using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.EnglishMission;
using ltwnc.Services.Ai;
using ltwnc.Services.Auth;
using ltwnc.Services.EnglishMission;
using ltwnc.Services.FlashcardSets;

namespace ltwnc.Controllers;

[Authorize]
public sealed class EnglishMissionController : Controller
{
    private readonly IEnglishMissionService _missionService;
    private readonly IFlashcardSetService _setService;
    private readonly ICurrentUser _currentUser;

    public EnglishMissionController(
        IEnglishMissionService missionService,
        IFlashcardSetService setService,
        ICurrentUser currentUser)
    {
        _missionService = missionService;
        _setService = setService;
        _currentUser = currentUser;
    }

    [HttpGet("/Study/{setId}/Mission")]
    public async Task<IActionResult> SelectTopic(int setId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null) return Challenge();
        FlashcardSet? set = await _setService.GetOwnedSetAsync(setId, userId);
        if (set == null) return RedirectToAction("Details", "FlashcardSet", new { id = setId });
        return View(new EnglishMissionTopicViewModel { SetId = setId, SetTitle = set.Title, Topics = _missionService.GetTopics() });
    }

    [HttpPost("/Study/{setId}/Mission/Start")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> Start(int setId, string topic, CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        try
        {
            EnglishMissionStartResult result = await _missionService.StartAsync(userId, setId, topic, cancellationToken);
            return RedirectToAction(nameof(Chat), new { setId, sessionId = result.Mission.StudySessionId });
        }
        catch (Exception exception) when (exception is ArgumentException or AiProviderUnavailableException or AiProviderConfigurationException)
        {
            TempData["MissionError"] = exception.Message;
            return RedirectToAction(nameof(SelectTopic), new { setId });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("/Study/{setId}/Mission/{sessionId:int}")]
    public async Task<IActionResult> Chat(int setId, int sessionId, CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (userId == null) return Challenge();
        try
        {
            EnglishMissionStartResult result = await _missionService.GetAsync(userId, setId, sessionId, cancellationToken);
            FlashcardSet? set = await _setService.GetOwnedSetAsync(setId, userId);
            if (result.Mission.Status == "Completed") return RedirectToAction(nameof(Result), new { setId, sessionId });
            return View(new EnglishMissionChatViewModel
            {
                SetId = setId,
                SetTitle = set?.Title ?? string.Empty,
                Mission = result.Mission,
                TargetWords = result.TargetWords,
                Turns = result.Turns
            });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("/Study/{setId}/Mission/{sessionId:int}/Respond")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> Respond(int setId, int sessionId, [FromForm] string clientTurnId, [FromForm] string userText, CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        try
        {
            EnglishMissionRespondResult result = await _missionService.RespondAsync(userId, setId, sessionId, clientTurnId, userText, cancellationToken);
            return Json(new
            {
                success = true,
                turn = new
                {
                    userText = result.Turn.UserText,
                    npcText = result.Turn.NpcText,
                    feedbackVi = result.Turn.FeedbackVi,
                    correctionEn = result.Turn.CorrectionEn,
                    correctionExplanationVi = result.Turn.CorrectionExplanationVi
                },
                targetWords = result.TargetWords.Select(word => new { word.Term, word.IsUsed }),
                completed = result.Mission.Status == "Completed",
                score = result.Mission.Score,
                resultUrl = Url.Action(nameof(Result), new { setId, sessionId })
            });
        }
        catch (Exception exception) when (exception is AiProviderUnavailableException or AiProviderConfigurationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, error = exception.Message, retryable = true });
        }
        catch (ArgumentException exception) { return BadRequest(new { success = false, error = exception.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("/Study/{setId}/Mission/{sessionId:int}/Result")]
    public async Task<IActionResult> Result(int setId, int sessionId, CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (userId == null) return Challenge();
        try
        {
            EnglishMissionStartResult result = await _missionService.GetAsync(userId, setId, sessionId, cancellationToken);
            FlashcardSet? set = await _setService.GetOwnedSetAsync(setId, userId);
            return View(new EnglishMissionChatViewModel
            {
                SetId = setId,
                SetTitle = set?.Title ?? string.Empty,
                Mission = result.Mission,
                TargetWords = result.TargetWords,
                Turns = result.Turns
            });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
