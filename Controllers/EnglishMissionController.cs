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

// Điều phối nhiệm vụ hội thoại tiếng Anh sử dụng AI cho bộ thẻ của người dùng.
[Authorize]
public sealed class EnglishMissionController : Controller
{
    // Các service xử lý nhiệm vụ, kiểm tra quyền sở hữu bộ thẻ và đọc người dùng hiện tại.
    private readonly IEnglishMissionService _missionService;
    private readonly IFlashcardSetService _setService;
    private readonly ICurrentUser _currentUser;

    // Nhận các service cần dùng qua dependency injection.
    public EnglishMissionController(
        IEnglishMissionService missionService,
        IFlashcardSetService setService,
        ICurrentUser currentUser)
    {
        // 1. Lưu các service để những action nhiệm vụ sử dụng.
        _missionService = missionService;
        _setService = setService;
        _currentUser = currentUser;
    }

    // Hiển thị danh sách chủ đề để người dùng chọn trước khi bắt đầu hội thoại.
    [HttpGet("/Study/{setId}/Mission")]
    public async Task<IActionResult> SelectTopic(int setId)
    {
        // 1. Kiểm tra người dùng đã đăng nhập.
        // 2. Xác nhận bộ thẻ thuộc sở hữu của người dùng.
        // 3. Lấy danh sách chủ đề và hiển thị trang lựa chọn.
        string? userId = _currentUser.UserId;
        if (userId == null) return Challenge();
        FlashcardSet? set = await _setService.GetOwnedSetAsync(setId, userId);
        if (set == null) return RedirectToAction("Details", "FlashcardSet", new { id = setId });
        return View(new EnglishMissionTopicViewModel { SetId = setId, SetTitle = set.Title, Topics = _missionService.GetTopics() });
    }

    // Tạo nhiệm vụ mới; lỗi cấu hình AI được đưa về trang chọn chủ đề.
    [HttpPost("/Study/{setId}/Mission/Start")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> Start(int setId, string topic, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra phiên đăng nhập.
        // 2. Tạo nhiệm vụ AI theo bộ thẻ và chủ đề đã chọn.
        // 3. Chuyển tới màn chat hoặc đưa lỗi về trang chọn chủ đề.
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

    // Hiển thị nội dung hội thoại hiện tại hoặc chuyển sang kết quả nếu đã hoàn thành.
    [HttpGet("/Study/{setId}/Mission/{sessionId:int}")]
    public async Task<IActionResult> Chat(int setId, int sessionId, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra phiên đăng nhập.
        // 2. Lấy nhiệm vụ, bộ thẻ và lịch sử hội thoại.
        // 3. Hiển thị chat hoặc chuyển sang kết quả nếu nhiệm vụ đã hoàn thành.
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
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // Gửi câu trả lời tới AI và trả JSON để giao diện cập nhật hội thoại tại chỗ.
    [HttpPost("/Study/{setId}/Mission/{sessionId:int}/Respond")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> Respond(int setId, int sessionId, [FromForm] string clientTurnId, [FromForm] string userText, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra người gửi đã đăng nhập.
        // 2. Gửi câu trả lời tới service để AI phản hồi và chấm điểm.
        // 3. Trả JSON cho giao diện hoặc mã lỗi HTTP phù hợp.
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
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // Hiển thị điểm và toàn bộ lượt hội thoại khi nhiệm vụ đã hoàn thành.
    [HttpGet("/Study/{setId}/Mission/{sessionId:int}/Result")]
    public async Task<IActionResult> Result(int setId, int sessionId, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra phiên đăng nhập.
        // 2. Lấy dữ liệu nhiệm vụ và xác nhận trạng thái đã hoàn thành.
        // 3. Hiển thị kết quả hoặc chuyển người dùng quay lại chat.
        string? userId = _currentUser.UserId;
        if (userId == null) return Challenge();
        try
        {
            EnglishMissionStartResult result = await _missionService.GetAsync(userId, setId, sessionId, cancellationToken);
            FlashcardSet? set = await _setService.GetOwnedSetAsync(setId, userId);
            if (result.Mission.Status != "Completed")
            {
                TempData["MissionError"] = "Mission chưa hoàn thành. Hãy tiếp tục hội thoại để xem kết quả.";
                return RedirectToAction(nameof(Chat), new { setId, sessionId });
            }

            return View(new EnglishMissionChatViewModel
            {
                SetId = setId,
                SetTitle = set?.Title ?? string.Empty,
                Mission = result.Mission,
                TargetWords = result.TargetWords,
                Turns = result.Turns
            });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
