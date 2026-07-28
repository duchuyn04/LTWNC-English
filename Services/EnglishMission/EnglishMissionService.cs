using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Ai;
using ltwnc.Services.Study;
using ltwnc.Services.StudyEvents;
using MissionEntity = ltwnc.Models.Entities.EnglishMission;

namespace ltwnc.Services.EnglishMission;

public sealed class EnglishMissionService : IEnglishMissionService
{
    private static readonly IReadOnlyList<EnglishMissionTopic> Topics =
    [
        new("airport", "Sân bay", "Báo thất lạc hành lý và mô tả chiếc vali."),
        new("restaurant", "Nhà hàng", "Gọi món, hỏi thành phần và xử lý một món ăn bị nhầm."),
        new("hotel", "Khách sạn", "Nhận phòng và giải quyết một vấn đề trong phòng."),
        new("interview", "Phỏng vấn", "Giới thiệu kinh nghiệm và trả lời câu hỏi tuyển dụng."),
        new("returns", "Đổi trả hàng", "Giải thích vấn đề và yêu cầu đổi một sản phẩm lỗi.")
    ];

    private const int MaxTargetWords = 5;
    private const int MaxTurns = 8;
    private readonly AppDbContext _context;
    private readonly IStudyService _studyService;
    private readonly IAiCompletionRouter _router;
    private readonly IStudyEventPublisher _studyEvents;
    private readonly TimeProvider _timeProvider;

    // Nhận các service học tập, AI, sự kiện và thời gian cần cho toàn bộ vòng đời Nhiệm vụ tiếng Anh.
    public EnglishMissionService(
        AppDbContext context,
        IStudyService studyService,
        IAiCompletionRouter router,
        IStudyEventPublisher studyEvents,
        TimeProvider timeProvider)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_studyService` để các phương thức khác sử dụng.
        _studyService = studyService;
        // 3. Lưu dependency `_router` để các phương thức khác sử dụng.
        _router = router;
        // 4. Lưu dependency `_studyEvents` để các phương thức khác sử dụng.
        _studyEvents = studyEvents;
        // 5. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    // Trả danh sách chủ đề cố định để controller không tự tạo nội dung nhiệm vụ.
    public IReadOnlyList<EnglishMissionTopic> GetTopics()
    {
        // 1. Trả `Topics` cho nơi gọi.
        return Topics;
    }

    // Kiểm tra quyền trên bộ thẻ, lấy từ mục tiêu và tạo Nhiệm vụ tiếng Anh mới bằng AI.
    public async Task<EnglishMissionStartResult> StartAsync(
        string userId,
        int setId,
        string topic,
        CancellationToken cancellationToken = default)
    {
        // 1. Tính giá trị và lưu vào `selectedTopic` để dùng ở bước tiếp theo.
        EnglishMissionTopic selectedTopic = Topics.FirstOrDefault(item => item.Id == topic)
            ?? throw new ArgumentException("Chủ đề không hợp lệ.");

        // 2. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync([setId], cancellationToken);
        // 3. Gọi `EnsureSetAccess` để thực hiện bước nghiệp vụ này.
        EnsureSetAccess(set, userId);

        // 4. Gọi `GetSettingsAsync` và lưu kết quả vào `settings`.
        UserStudySettings settings = await _studyService.GetSettingsAsync(userId);
        // 5. Gọi `ToList` và lưu kết quả vào `cards`.
        List<Flashcard> cards = (await _studyService.GetCardsForModeAsync(
                StudyMode.EnglishMission,
                setId,
                settings,
                userId))
            .Take(MaxTargetWords)
            .ToList();
        // 6. Kiểm tra `cards.Count < 3` để chọn nhánh xử lý phù hợp.
        if (cards.Count < 3) throw new ArgumentException("English Mission cần ít nhất 3 thẻ trong bộ thẻ.");

        // 7. Gọi `Join` và lưu kết quả vào `targetWords`.
        string targetWords = string.Join(", ", cards.Select(card => card.FrontText));
        // 8. Gọi `CompleteAsync` và lưu kết quả vào `ai`.
        AiCompletionResult ai = await _router.CompleteAsync(
            new AiCompletionRequest(
                BuildStartSystemPrompt(),
                $"Chủ đề: {selectedTopic.Name}\nMô tả: {selectedTopic.Description}\nTừ mục tiêu: {targetWords}\nHãy tạo mission.",
                1400),
            IsValidStartPayload,
            cancellationToken);

        // 9. Gọi `Parse` và lưu kết quả vào `payload`.
        StartPayload payload = Parse<StartPayload>(ai.Content, "AI không trả được dữ liệu khởi tạo mission hợp lệ.");
        // 10. Tính giá trị và lưu vào `goals` để dùng ở bước tiếp theo.
        List<GoalPayload> goals = payload.Goals?.Where(goal => !string.IsNullOrWhiteSpace(goal.Id)).Take(6).ToList() ?? [];
        // 11. Kiểm tra `string.IsNullOrWhiteSpace(payload.Title) || string.IsNullOrWhiteSpa...` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(payload.Title)
            || string.IsNullOrWhiteSpace(payload.Situation)
            || string.IsNullOrWhiteSpace(payload.NpcName)
            || string.IsNullOrWhiteSpace(payload.OpeningLine)
            || goals.Count == 0)
        {
            // 12. Dừng xử lý và phát sinh lỗi `new AiProviderUnavailableException("AI trả mission thiếu dữ liệu bắ...`.
            throw new AiProviderUnavailableException("AI trả mission thiếu dữ liệu bắt buộc.");
        }

        // 13. Khởi tạo `session` với dữ liệu ban đầu cần thiết.
        StudySession session = new()
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = StudyMode.EnglishMission,
            PlannedItemCount = cards.Count,
            StartedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        // 14. Khởi tạo `mission` với dữ liệu ban đầu cần thiết.
        MissionEntity mission = new()
        {
            StudySession = session,
            Topic = selectedTopic.Id,
            Title = Limit(payload.Title, 200),
            Situation = Limit(payload.Situation, 4000),
            NpcName = Limit(payload.NpcName, 120),
            NpcRole = Limit(payload.NpcRole ?? "Đối tác hội thoại", 200),
            OpeningLine = Limit(payload.OpeningLine, 2000),
            GoalsJson = JsonSerializer.Serialize(goals),
            Status = "Active"
        };

        // 15. Duyệt từng `card` trong `cards` để xử lý lần lượt.
        foreach (Flashcard card in cards)
        {
            // 16. Gọi `Add` để thực hiện bước nghiệp vụ này.
            mission.TargetWords.Add(new EnglishMissionTargetWord
            {
                FlashcardId = card.Id,
                Term = Limit(card.FrontText, 160),
                Definition = Limit(card.BackText, 500),
                PartOfSpeech = LimitNullable(card.PartOfSpeech, 80),
                ExampleSentence = LimitNullable(card.ExampleSentence, 1000)
            });
        }
        // 17. Gọi `Add` để thực hiện bước nghiệp vụ này.
        _context.EnglishMissions.Add(mission);
        // 18. Gọi `SaveWithCurrentSetAccessAsync` để thực hiện bước nghiệp vụ này.
        await SaveWithCurrentSetAccessAsync(userId, setId, cancellationToken);
        // 19. Trả kết quả từ `ToResult` cho nơi gọi.
        return ToResult(mission, []);
    }

    // Tải nhiệm vụ thuộc đúng người học cùng toàn bộ từ mục tiêu và lượt hội thoại đã lưu.
    public async Task<EnglishMissionStartResult> GetAsync(
        string userId,
        int setId,
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `GetMissionAsync` và lưu kết quả vào `mission`.
        MissionEntity mission = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
        // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new EnglishMissionStartResult
        {
            Mission = mission,
            TargetWords = mission.TargetWords.OrderBy(word => word.Id).ToList(),
            Turns = mission.Turns.OrderBy(turn => turn.TurnNumber).ToList()
        };
    }

    // Gửi một lượt hội thoại, lọc dữ liệu AI và lưu kết quả idempotent theo clientTurnId.
    public async Task<EnglishMissionRespondResult> RespondAsync(
        string userId,
        int setId,
        int sessionId,
        string clientTurnId,
        string userText,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(userText) || userText.Length > 1000` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(userText) || userText.Length > 1000)
            // 2. Dừng xử lý và phát sinh lỗi `new ArgumentException("Câu trả lời phải từ 1 đến 1000 ký tự.")`.
            throw new ArgumentException("Câu trả lời phải từ 1 đến 1000 ký tự.");
        // 3. Kiểm tra `string.IsNullOrWhiteSpace(clientTurnId) || clientTurnId.Length > 64` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(clientTurnId) || clientTurnId.Length > 64)
            // 4. Dừng xử lý và phát sinh lỗi `new ArgumentException("Mã lượt hội thoại không hợp lệ.")`.
            throw new ArgumentException("Mã lượt hội thoại không hợp lệ.");

        // 5. Gọi `GetMissionAsync` và lưu kết quả vào `mission`.
        MissionEntity mission = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
        // 6. Gọi `FirstOrDefault` và lưu kết quả vào `existing`.
        EnglishMissionTurn? existing = mission.Turns.FirstOrDefault(turn => turn.ClientTurnId == clientTurnId);
        // 7. Kiểm tra `existing != null` để chọn nhánh xử lý phù hợp.
        if (existing != null)
        {
            // 8. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new EnglishMissionRespondResult
            {
                Turn = existing,
                Mission = mission,
                TargetWords = mission.TargetWords.OrderBy(word => word.Id).ToList()
            };
        }
        // 9. Kiểm tra `mission.Status != "Active"` để chọn nhánh xử lý phù hợp.
        if (mission.Status != "Active") throw new ArgumentException("Mission này đã kết thúc.");
        // 10. Kiểm tra `mission.TurnCount >= MaxTurns` để chọn nhánh xử lý phù hợp.
        if (mission.TurnCount >= MaxTurns) throw new ArgumentException("Mission đã đạt số lượt tối đa.");

        // 11. Gọi `Parse` và lưu kết quả vào `goals`.
        List<GoalPayload> goals = Parse<List<GoalPayload>>(mission.GoalsJson, "Dữ liệu mục tiêu mission không hợp lệ.");
        // 12. Gọi `ToList` và lưu kết quả vào `turns`.
        List<EnglishMissionTurn> turns = mission.Turns.OrderBy(turn => turn.TurnNumber).ToList();
        // 13. Gọi `Join` và lưu kết quả vào `transcript`.
        string transcript = string.Join("\n", turns.Select(turn => $"Người học: {turn.UserText}\nNPC: {turn.NpcText}"));
        // 14. Gọi `Join` và lưu kết quả vào `words`.
        string words = string.Join(", ", mission.TargetWords.Select(word => word.Term));

        // 15. Gọi `CompleteAsync` và lưu kết quả vào `ai`.
        AiCompletionResult ai = await _router.CompleteAsync(
            new AiCompletionRequest(
                BuildTurnSystemPrompt(),
                $"Mission: {mission.Title}\nTình huống: {mission.Situation}\nNPC: {mission.NpcName} - {mission.NpcRole}\nMục tiêu: {JsonSerializer.Serialize(goals)}\nTừ mục tiêu: {words}\nLịch sử:\n{transcript}\nNgười học vừa nói: {userText}",
                1200),
            IsValidTurnPayload,
            cancellationToken);
        // 16. Gọi `Parse` và lưu kết quả vào `payload`.
        TurnPayload payload = Parse<TurnPayload>(ai.Content, "AI không trả được phản hồi hội thoại hợp lệ.");

        // 17. Gọi `ToHashSet` và lưu kết quả vào `validWords`.
        HashSet<string> validWords = mission.TargetWords.Select(word => word.Term).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // 18. Gọi `ToHashSet` và lưu kết quả vào `usedWords`.
        HashSet<string> usedWords = (payload.UsedTargetWords ?? []).Where(validWords.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // 19. Gọi `ToHashSet` và lưu kết quả vào `validGoals`.
        HashSet<string> validGoals = goals.Select(goal => goal.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // 20. Gọi `ToHashSet` và lưu kết quả vào `achievedGoals`.
        HashSet<string> achievedGoals = (payload.AchievedGoalIds ?? []).Where(validGoals.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 21. Khởi tạo `turn` với dữ liệu ban đầu cần thiết.
        EnglishMissionTurn turn = new()
        {
            EnglishMissionId = mission.Id,
            TurnNumber = mission.TurnCount + 1,
            ClientTurnId = clientTurnId,
            UserText = userText.Trim(),
            NpcText = Limit(payload.NpcReply, 2000),
            FeedbackVi = LimitNullable(payload.FeedbackVi, 1000),
            CorrectionEn = LimitNullable(payload.CorrectionEn, 1000),
            CorrectionExplanationVi = LimitNullable(payload.CorrectionExplanationVi, 1000),
            UsedWordsJson = JsonSerializer.Serialize(usedWords),
            AchievedGoalsJson = JsonSerializer.Serialize(achievedGoals),
            ProviderName = ai.ProviderName,
            ModelId = ai.ModelId
        };
        // 22. Gọi `Add` để thực hiện bước nghiệp vụ này.
        _context.EnglishMissionTurns.Add(turn);
        // 23. Duyệt từng `word` trong `mission.TargetWords.Where(word => usedWords.Contains(word.Term))` để xử lý lần lượt.
        foreach (EnglishMissionTargetWord word in mission.TargetWords.Where(word => usedWords.Contains(word.Term)))
        {
            // 24. Cập nhật `word.IsUsed` bằng giá trị mới.
            word.IsUsed = true;
            // 25. Cập nhật `word.FirstUsedTurn` bằng giá trị mới.
            word.FirstUsedTurn ??= turn.TurnNumber;
        }
        // 26. Cập nhật bộ đếm hoặc trạng thái `mission.TurnCount`.
        mission.TurnCount++;
        // 27. Gọi `ToList` và lưu kết quả vào `allAchieved`.
        List<string> allAchieved = turns
            .SelectMany(item => Parse<List<string>>(item.AchievedGoalsJson, "[]"))
            .Concat(achievedGoals)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        // 28. Tính giá trị và lưu vào `complete` để dùng ở bước tiếp theo.
        bool complete = payload.MissionCompleted == true || goals.All(goal => allAchieved.Contains(goal.Id, StringComparer.OrdinalIgnoreCase));
        // 29. Kiểm tra `complete || mission.TurnCount >= MaxTurns` để chọn nhánh xử lý phù hợp.
        if (complete || mission.TurnCount >= MaxTurns)
        {
            // 30. Cập nhật `mission.Status` bằng giá trị mới.
            mission.Status = "Completed";
            // 31. Cập nhật `mission.CompletedAt` bằng giá trị mới.
            mission.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
            // 32. Cập nhật `mission.Score` bằng giá trị mới.
            mission.Score = CalculateScore(goals.Count, allAchieved.Count, mission.TargetWords.Count(word => word.IsUsed), mission.TurnCount);
            // 33. Cập nhật `mission.StudySession!.CompletedAt` bằng giá trị mới.
            mission.StudySession!.CompletedAt = mission.CompletedAt;
            // 34. Cập nhật `mission.StudySession.DurationSeconds` bằng giá trị mới.
            mission.StudySession.DurationSeconds = (int)Math.Clamp((mission.CompletedAt.Value - mission.StudySession.StartedAt).TotalSeconds, 0, 14400);
            // 35. Cập nhật `mission.StudySession.Score` bằng giá trị mới.
            mission.StudySession.Score = mission.Score;
        }
        // 36. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 37. Gọi `SaveWithCurrentSetAccessAsync` để thực hiện bước nghiệp vụ này.
            await SaveWithCurrentSetAccessAsync(userId, setId, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // 38. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 39. Gọi `GetMissionAsync` và lưu kết quả vào `latest`.
            MissionEntity latest = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
            // 40. Gọi `FirstOrDefault` và lưu kết quả vào `persisted`.
            EnglishMissionTurn? persisted = latest.Turns.FirstOrDefault(item => item.ClientTurnId == clientTurnId);
            // 41. Kiểm tra `persisted != null` để chọn nhánh xử lý phù hợp.
            if (persisted != null)
            {
                // 42. Tạo và trả đối tượng kết quả cho nơi gọi.
                return new EnglishMissionRespondResult
                {
                    Turn = persisted,
                    Mission = latest,
                    TargetWords = latest.TargetWords.OrderBy(word => word.Id).ToList()
                };
            }
            // 43. Dừng xử lý và phát sinh lỗi `new ArgumentException("Mission vừa được cập nhật ở một yêu cầu khác...`.
            throw new ArgumentException("Mission vừa được cập nhật ở một yêu cầu khác. Vui lòng gửi lại câu trả lời.");
        }
        catch (DbUpdateException)
        {
            // 44. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 45. Gọi `GetMissionAsync` và lưu kết quả vào `latest`.
            MissionEntity latest = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
            // 46. Gọi `FirstOrDefault` và lưu kết quả vào `persisted`.
            EnglishMissionTurn? persisted = latest.Turns.FirstOrDefault(item => item.ClientTurnId == clientTurnId);
            // 47. Kiểm tra `persisted != null` để chọn nhánh xử lý phù hợp.
            if (persisted != null)
            {
                // 48. Tạo và trả đối tượng kết quả cho nơi gọi.
                return new EnglishMissionRespondResult
                {
                    Turn = persisted,
                    Mission = latest,
                    TargetWords = latest.TargetWords.OrderBy(word => word.Id).ToList()
                };
            }
            // 49. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        // 50. Kiểm tra `mission.Status == "Completed"` để chọn nhánh xử lý phù hợp.
        if (mission.Status == "Completed")
        {
            // 51. Gọi `PublishCompletedAsync` để thực hiện bước nghiệp vụ này.
            await PublishCompletedAsync(mission, cancellationToken);
        }
        // 52. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new EnglishMissionRespondResult { Turn = turn, Mission = mission, TargetWords = mission.TargetWords.OrderBy(word => word.Id).ToList() };
    }

    // Kết thúc nhiệm vụ thủ công, cập nhật phiên học và phát sự kiện hoàn thành đúng một lần.
    public async Task CompleteAsync(string userId, int setId, int sessionId, CancellationToken cancellationToken = default)
    {
        // 1. Gọi `GetMissionAsync` và lưu kết quả vào `mission`.
        MissionEntity mission = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
        // 2. Kiểm tra `mission.Status == "Completed"` để chọn nhánh xử lý phù hợp.
        if (mission.Status == "Completed") return;
        // 3. Cập nhật `mission.Status` bằng giá trị mới.
        mission.Status = "Completed";
        // 4. Cập nhật `mission.CompletedAt` bằng giá trị mới.
        mission.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
        // 5. Gọi `Parse` và lưu kết quả vào `goals`.
        List<GoalPayload> goals = Parse<List<GoalPayload>>(mission.GoalsJson, "[]");
        // 6. Gọi `ToHashSet` và lưu kết quả vào `validGoalIds`.
        HashSet<string> validGoalIds = goals
            .Select(goal => goal.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // 7. Gọi `Count` và lưu kết quả vào `achievedGoals`.
        int achievedGoals = mission.Turns
            .SelectMany(turn => Parse<List<string>>(turn.AchievedGoalsJson, "[]"))
            .Where(validGoalIds.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        // 8. Cập nhật `mission.Score` bằng giá trị mới.
        mission.Score = CalculateScore(
            goals.Count,
            achievedGoals,
            mission.TargetWords.Count(word => word.IsUsed),
            mission.TurnCount);
        // 9. Cập nhật `mission.StudySession!.CompletedAt` bằng giá trị mới.
        mission.StudySession!.CompletedAt = mission.CompletedAt;
        // 10. Cập nhật `mission.StudySession.DurationSeconds` bằng giá trị mới.
        mission.StudySession.DurationSeconds = (int)Math.Clamp((mission.CompletedAt.Value - mission.StudySession.StartedAt).TotalSeconds, 0, 14400);
        // 11. Cập nhật `mission.StudySession.Score` bằng giá trị mới.
        mission.StudySession.Score = mission.Score;
        // 12. Gọi `SaveWithCurrentSetAccessAsync` để thực hiện bước nghiệp vụ này.
        await SaveWithCurrentSetAccessAsync(userId, setId, cancellationToken);
        // 13. Gọi `PublishCompletedAsync` để thực hiện bước nghiệp vụ này.
        await PublishCompletedAsync(mission, cancellationToken);
    }

    // Tải nhiệm vụ cùng navigation và xác nhận nó thuộc đúng user, bộ thẻ và phiên học yêu cầu.
    private async Task<MissionEntity> GetMissionAsync(string userId, int setId, int sessionId, CancellationToken cancellationToken)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `mission`.
        MissionEntity? mission = await _context.EnglishMissions
            .Include(item => item.TargetWords)
            .Include(item => item.Turns)
            .Include(item => item.StudySession)
                .ThenInclude(session => session!.FlashcardSet)
            .FirstOrDefaultAsync(item => item.StudySessionId == sessionId, cancellationToken);
        // 2. Kiểm tra `mission?.StudySession == null || mission.StudySession.UserId != use...` để chọn nhánh xử lý phù hợp.
        if (mission?.StudySession == null || mission.StudySession.UserId != userId || mission.StudySession.FlashcardSetId != setId)
            // 3. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền truy cập mission nà...`.
            throw new UnauthorizedAccessException("Không có quyền truy cập mission này.");

        // Dùng cùng quy tắc với lúc bắt đầu để thay đổi cách ly hoặc quyền riêng tư có hiệu lực ngay.
        // 4. Gọi `EnsureSetAccess` để thực hiện bước nghiệp vụ này.
        EnsureSetAccess(mission.StudySession.FlashcardSet, userId);

        // 5. Trả `mission` cho nơi gọi.
        return mission;
    }

    // Kiểm tra lại quyền truy cập trong cùng transaction với lần ghi để không lọt thay đổi kiểm duyệt đồng thời.
    private async Task SaveWithCurrentSetAccessAsync(
        string userId,
        int setId,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra `!_context.Database.IsRelational()` để chọn nhánh xử lý phù hợp.
        if (!_context.Database.IsRelational())
        {
            // 2. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
            try
            {
                // 3. Gọi `EnsureCurrentSetAccessAsync` để thực hiện bước nghiệp vụ này.
                await EnsureCurrentSetAccessAsync(userId, setId, cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                // 4. Gọi `Clear` để thực hiện bước nghiệp vụ này.
                _context.ChangeTracker.Clear();
                // 5. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
                throw;
            }

            // 6. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 7. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 8. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
        // 9. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 10. Gọi `EnsureCurrentSetAccessAsync` để thực hiện bước nghiệp vụ này.
            await EnsureCurrentSetAccessAsync(userId, setId, cancellationToken);
            // 11. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 12. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            // 13. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
            await transaction.RollbackAsync(cancellationToken);
            // 14. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 15. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        catch (KeyNotFoundException)
        {
            // 16. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
            await transaction.RollbackAsync(cancellationToken);
            // 17. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 18. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        catch
        {
            // 19. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
            await transaction.RollbackAsync(cancellationToken);
            // 20. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
    }

    // Đọc lại bộ thẻ không tracking để không dùng trạng thái cũ đã được nạp trước khi gọi AI.
    private async Task EnsureCurrentSetAccessAsync(
        string userId,
        int setId,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `currentSet`.
        FlashcardSet? currentSet = await _context.FlashcardSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == setId, cancellationToken);
        // 2. Gọi `EnsureSetAccess` để thực hiện bước nghiệp vụ này.
        EnsureSetAccess(currentSet, userId);
    }

    // Áp dụng một quy tắc chung: tác giả được truy cập, người khác cần bộ công khai và không bị cách ly.
    private static void EnsureSetAccess(FlashcardSet? set, string userId)
    {
        // 1. Kiểm tra `set == null` để chọn nhánh xử lý phù hợp.
        if (set == null)
        {
            // 2. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Bộ thẻ không tồn tại.")`.
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        }

        // 3. Gọi `Equals` và lưu kết quả vào `isOwner`.
        bool isOwner = string.Equals(set.UserId, userId, StringComparison.Ordinal);
        // 4. Kiểm tra `set.ModerationStatus == FlashcardSetModerationStatus.Quarantined &&...` để chọn nhánh xử lý phù hợp.
        if (set.ModerationStatus == FlashcardSetModerationStatus.Quarantined && !isOwner)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException( "Bộ thẻ đang bị cách ly và không t...`.
            throw new UnauthorizedAccessException(
                "Bộ thẻ đang bị cách ly và không thể dùng để học công khai.");
        }

        // 6. Kiểm tra `!set.IsPublic && !isOwner` để chọn nhánh xử lý phù hợp.
        if (!set.IsPublic && !isOwner)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền học bộ thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");
        }
    }

    // Chuyển entity sang kết quả bắt đầu với thứ tự từ mục tiêu ổn định.
    private static EnglishMissionStartResult ToResult(MissionEntity mission, IReadOnlyList<EnglishMissionTurn> turns)
    {
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new() { Mission = mission, TargetWords = mission.TargetWords.OrderBy(word => word.Id).ToList(), Turns = turns };
    }

    // Tính điểm từ mục tiêu, từ đã dùng và số lượt rồi giới hạn trong thang 0 đến 100.
    private static int CalculateScore(int goalCount, int achievedGoals, int usedWords, int turns)
    {
        // 1. Tính giá trị và lưu vào `goalScore` để dùng ở bước tiếp theo.
        int goalScore = 0;
        // 2. Kiểm tra `goalCount > 0` để chọn nhánh xử lý phù hợp.
        if (goalCount > 0)
        {
            // 3. Cập nhật `goalScore` bằng giá trị mới.
            goalScore = achievedGoals * 40 / goalCount;
        }

        // 4. Gọi `Min` và lưu kết quả vào `vocabularyScore`.
        int vocabularyScore = Math.Min(30, usedWords * 6);
        // 5. Tính giá trị và lưu vào `completionBonus` để dùng ở bước tiếp theo.
        int completionBonus = 0;
        // 6. Kiểm tra `achievedGoals > 0` để chọn nhánh xử lý phù hợp.
        if (achievedGoals > 0)
        {
            // 7. Cập nhật `completionBonus` bằng giá trị mới.
            completionBonus = 20;
        }

        // 8. Gọi `Max` và lưu kết quả vào `turnScore`.
        int turnScore = Math.Max(0, 10 - Math.Max(0, turns - 3));
        // 9. Trả kết quả từ `Clamp` cho nơi gọi.
        return Math.Clamp(goalScore + vocabularyScore + completionBonus + turnScore, 0, 100);
    }

    // Phát sự kiện hoàn thành để các observer cập nhật tiến độ và thành tích từ cùng dữ liệu phiên học.
    private async Task PublishCompletedAsync(MissionEntity mission, CancellationToken cancellationToken)
    {
        // 1. Gọi `PublishAsync` để thực hiện bước nghiệp vụ này.
        await _studyEvents.PublishAsync(new StudySessionCompletedEvent(
            mission.StudySession!.UserId,
            mission.CompletedAt!.Value,
            mission.StudySession.FlashcardSetId,
            mission.StudySessionId,
            StudyMode.EnglishMission,
            mission.Score), cancellationToken);
    }

    // Kiểm tra payload khởi tạo có đủ các trường bắt buộc trước khi router chấp nhận phản hồi AI.
    private static bool IsValidStartPayload(string content)
    {
        // 1. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 2. Gọi `Parse` và lưu kết quả vào `payload`.
            StartPayload payload = Parse<StartPayload>(content, "invalid");
            // 3. Trả `!string.IsNullOrWhiteSpace(payload.Title) && !string.IsNullOrWhiteS...` cho nơi gọi.
            return !string.IsNullOrWhiteSpace(payload.Title)
                && !string.IsNullOrWhiteSpace(payload.Situation)
                && !string.IsNullOrWhiteSpace(payload.NpcName)
                && !string.IsNullOrWhiteSpace(payload.OpeningLine)
                && payload.Goals?.Any(goal => !string.IsNullOrWhiteSpace(goal.Id)) == true;
        }
        catch (AiProviderUnavailableException) { return false; }
    }

    // Kiểm tra payload lượt hội thoại có câu trả lời NPC hợp lệ.
    private static bool IsValidTurnPayload(string content)
    {
        // 1. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try { return !string.IsNullOrWhiteSpace(Parse<TurnPayload>(content, "invalid").NpcReply); }
        catch (AiProviderUnavailableException) { return false; }
    }

    // Parse JSON đã làm sạch và chuyển lỗi định dạng thành lỗi AI thống nhất cho tầng trên.
    private static T Parse<T>(string content, string error)
    {
        // 1. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 2. Gọi `Parse` và lưu kết quả vào `document`.
            using JsonDocument document = JsonDocument.Parse(CleanJson(content));
            // 3. Gọi `Deserialize` và lưu kết quả vào `result`.
            T? result = document.RootElement.Deserialize<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            // 4. Trả `result ?? throw new InvalidOperationException()` cho nơi gọi.
            return result ?? throw new InvalidOperationException();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new AiProviderUnavailableException(error)`.
            throw new AiProviderUnavailableException(error);
        }
    }

    // Bỏ code fence nếu nhà cung cấp bọc JSON trong Markdown.
    private static string CleanJson(string content)
    {
        // 1. Gọi `Trim` và lưu kết quả vào `value`.
        string value = content.Trim();
        // 2. Kiểm tra `value.StartsWith("```", StringComparison.Ordinal)` để chọn nhánh xử lý phù hợp.
        if (value.StartsWith("```", StringComparison.Ordinal))
        {
            // 3. Gọi `IndexOf` và lưu kết quả vào `firstNewLine`.
            int firstNewLine = value.IndexOf('\n');
            // 4. Gọi `LastIndexOf` và lưu kết quả vào `end`.
            int end = value.LastIndexOf("```", StringComparison.Ordinal);
            // 5. Kiểm tra `firstNewLine >= 0 && end > firstNewLine` để chọn nhánh xử lý phù hợp.
            if (firstNewLine >= 0 && end > firstNewLine) value = value[(firstNewLine + 1)..end];
        }
        // 6. Trả kết quả từ `Trim` cho nơi gọi.
        return value.Trim();
    }

    // Cắt chuỗi theo giới hạn cột nhưng giữ nguyên chuỗi ngắn hơn.
    private static string Limit(string value, int max)
    {
        // 1. Kiểm tra `value.Length <= max` để chọn nhánh xử lý phù hợp.
        if (value.Length <= max)
        {
            // 2. Trả `value` cho nơi gọi.
            return value;
        }

        // 3. Trả `value[..max]` cho nơi gọi.
        return value[..max];
    }

    // Chuẩn hóa chuỗi tùy chọn trước khi áp dụng giới hạn độ dài.
    private static string? LimitNullable(string? value, int max)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(value)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(value))
        {
            // 2. Trả `null` cho nơi gọi.
            return null;
        }

        // 3. Trả kết quả từ `Limit` cho nơi gọi.
        return Limit(value, max);
    }

    // Tạo system prompt cố định cho bước khởi tạo nhiệm vụ.
    private static string BuildStartSystemPrompt()
    {
        // 1. Trả `"Bạn là biên kịch English Mission. Chỉ trả về JSON hợp lệ, không ma...` cho nơi gọi.
        return "Bạn là biên kịch English Mission. Chỉ trả về JSON hợp lệ, không markdown, theo schema: {title:string,situation:string,npcName:string,npcRole:string,openingLine:string,goals:[{id:string,descriptionVi:string}]}. Viết openingLine bằng tiếng Anh, các trường còn lại bằng tiếng Việt trừ title nếu tự nhiên. Không thêm trường khác.";
    }

    // Tạo system prompt cố định cho từng lượt hội thoại.
    private static string BuildTurnSystemPrompt()
    {
        // 1. Trả `"Bạn là gia sư hội thoại tiếng Anh. Chỉ trả JSON hợp lệ theo schema...` cho nơi gọi.
        return "Bạn là gia sư hội thoại tiếng Anh. Chỉ trả JSON hợp lệ theo schema: {npcReply:string,feedbackVi:string,correctionEn:string|null,correctionExplanationVi:string|null,usedTargetWords:string[],achievedGoalIds:string[],missionCompleted:boolean}. npcReply bằng tiếng Anh, phản hồi và giải thích bằng tiếng Việt. Không tự tạo goal hoặc từ không có trong danh sách.";
    }

    private sealed class StartPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Situation { get; set; } = string.Empty;
        public string NpcName { get; set; } = string.Empty;
        public string? NpcRole { get; set; }
        public string OpeningLine { get; set; } = string.Empty;
        public List<GoalPayload>? Goals { get; set; }
    }
    private sealed class GoalPayload { public string Id { get; set; } = string.Empty; public string DescriptionVi { get; set; } = string.Empty; }
    private sealed class TurnPayload
    {
        public string NpcReply { get; set; } = string.Empty;
        public string? FeedbackVi { get; set; }
        public string? CorrectionEn { get; set; }
        public string? CorrectionExplanationVi { get; set; }
        public List<string>? UsedTargetWords { get; set; }
        public List<string>? AchievedGoalIds { get; set; }
        public bool? MissionCompleted { get; set; }
    }
}
