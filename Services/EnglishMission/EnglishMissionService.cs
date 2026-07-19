using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
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

    public EnglishMissionService(
        AppDbContext context,
        IStudyService studyService,
        IAiCompletionRouter router,
        IStudyEventPublisher studyEvents,
        TimeProvider timeProvider)
    {
        _context = context;
        _studyService = studyService;
        _router = router;
        _studyEvents = studyEvents;
        _timeProvider = timeProvider;
    }

    public IReadOnlyList<EnglishMissionTopic> GetTopics() => Topics;

    public async Task<EnglishMissionStartResult> StartAsync(
        string userId,
        int setId,
        string topic,
        CancellationToken cancellationToken = default)
    {
        EnglishMissionTopic selectedTopic = Topics.FirstOrDefault(item => item.Id == topic)
            ?? throw new ArgumentException("Chủ đề không hợp lệ.");

        FlashcardSet? set = await _context.FlashcardSets.FindAsync([setId], cancellationToken);
        if (set == null) throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        if (!set.IsPublic && set.UserId != userId) throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");

        UserStudySettings settings = await _studyService.GetSettingsAsync(userId);
        List<Flashcard> cards = (await _studyService.GetCardsForModeAsync(
                StudyMode.EnglishMission,
                setId,
                settings,
                userId))
            .Take(MaxTargetWords)
            .ToList();
        if (cards.Count < 3) throw new ArgumentException("English Mission cần ít nhất 3 thẻ trong bộ thẻ.");

        string targetWords = string.Join(", ", cards.Select(card => card.FrontText));
        AiCompletionResult ai = await _router.CompleteAsync(
            new AiCompletionRequest(
                BuildStartSystemPrompt(),
                $"Chủ đề: {selectedTopic.Name}\nMô tả: {selectedTopic.Description}\nTừ mục tiêu: {targetWords}\nHãy tạo mission.",
                1400),
            IsValidStartPayload,
            cancellationToken);

        StartPayload payload = Parse<StartPayload>(ai.Content, "AI không trả được dữ liệu khởi tạo mission hợp lệ.");
        List<GoalPayload> goals = payload.Goals?.Where(goal => !string.IsNullOrWhiteSpace(goal.Id)).Take(6).ToList() ?? [];
        if (string.IsNullOrWhiteSpace(payload.Title)
            || string.IsNullOrWhiteSpace(payload.Situation)
            || string.IsNullOrWhiteSpace(payload.NpcName)
            || string.IsNullOrWhiteSpace(payload.OpeningLine)
            || goals.Count == 0)
        {
            throw new AiProviderUnavailableException("AI trả mission thiếu dữ liệu bắt buộc.");
        }

        StudySession session = new()
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = StudyMode.EnglishMission,
            StartedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
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

        foreach (Flashcard card in cards)
        {
            mission.TargetWords.Add(new EnglishMissionTargetWord
            {
                FlashcardId = card.Id,
                Term = Limit(card.FrontText, 160),
                Definition = Limit(card.BackText, 500),
                PartOfSpeech = LimitNullable(card.PartOfSpeech, 80),
                ExampleSentence = LimitNullable(card.ExampleSentence, 1000)
            });
        }
        _context.EnglishMissions.Add(mission);
        await _context.SaveChangesAsync(cancellationToken);
        return ToResult(mission, []);
    }

    public async Task<EnglishMissionStartResult> GetAsync(
        string userId,
        int setId,
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        MissionEntity mission = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
        return new EnglishMissionStartResult
        {
            Mission = mission,
            TargetWords = mission.TargetWords.OrderBy(word => word.Id).ToList(),
            Turns = mission.Turns.OrderBy(turn => turn.TurnNumber).ToList()
        };
    }

    public async Task<EnglishMissionRespondResult> RespondAsync(
        string userId,
        int setId,
        int sessionId,
        string clientTurnId,
        string userText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userText) || userText.Length > 1000)
            throw new ArgumentException("Câu trả lời phải từ 1 đến 1000 ký tự.");
        if (string.IsNullOrWhiteSpace(clientTurnId) || clientTurnId.Length > 64)
            throw new ArgumentException("Mã lượt hội thoại không hợp lệ.");

        MissionEntity mission = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
        EnglishMissionTurn? existing = mission.Turns.FirstOrDefault(turn => turn.ClientTurnId == clientTurnId);
        if (existing != null)
        {
            return new EnglishMissionRespondResult
            {
                Turn = existing,
                Mission = mission,
                TargetWords = mission.TargetWords.OrderBy(word => word.Id).ToList()
            };
        }
        if (mission.Status != "Active") throw new ArgumentException("Mission này đã kết thúc.");
        if (mission.TurnCount >= MaxTurns) throw new ArgumentException("Mission đã đạt số lượt tối đa.");

        List<GoalPayload> goals = Parse<List<GoalPayload>>(mission.GoalsJson, "Dữ liệu mục tiêu mission không hợp lệ.");
        List<EnglishMissionTurn> turns = mission.Turns.OrderBy(turn => turn.TurnNumber).ToList();
        string transcript = string.Join("\n", turns.Select(turn => $"Người học: {turn.UserText}\nNPC: {turn.NpcText}"));
        string words = string.Join(", ", mission.TargetWords.Select(word => word.Term));

        AiCompletionResult ai = await _router.CompleteAsync(
            new AiCompletionRequest(
                BuildTurnSystemPrompt(),
                $"Mission: {mission.Title}\nTình huống: {mission.Situation}\nNPC: {mission.NpcName} - {mission.NpcRole}\nMục tiêu: {JsonSerializer.Serialize(goals)}\nTừ mục tiêu: {words}\nLịch sử:\n{transcript}\nNgười học vừa nói: {userText}",
                1200),
            IsValidTurnPayload,
            cancellationToken);
        TurnPayload payload = Parse<TurnPayload>(ai.Content, "AI không trả được phản hồi hội thoại hợp lệ.");

        HashSet<string> validWords = mission.TargetWords.Select(word => word.Term).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> usedWords = (payload.UsedTargetWords ?? []).Where(validWords.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> validGoals = goals.Select(goal => goal.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> achievedGoals = (payload.AchievedGoalIds ?? []).Where(validGoals.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        _context.EnglishMissionTurns.Add(turn);
        foreach (EnglishMissionTargetWord word in mission.TargetWords.Where(word => usedWords.Contains(word.Term)))
        {
            word.IsUsed = true;
            word.FirstUsedTurn ??= turn.TurnNumber;
        }
        mission.TurnCount++;
        List<string> allAchieved = turns
            .SelectMany(item => Parse<List<string>>(item.AchievedGoalsJson, "[]"))
            .Concat(achievedGoals)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        bool complete = payload.MissionCompleted == true || goals.All(goal => allAchieved.Contains(goal.Id, StringComparer.OrdinalIgnoreCase));
        if (complete || mission.TurnCount >= MaxTurns)
        {
            mission.Status = "Completed";
            mission.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
            mission.Score = CalculateScore(goals.Count, allAchieved.Count, mission.TargetWords.Count(word => word.IsUsed), mission.TurnCount);
            mission.StudySession!.CompletedAt = mission.CompletedAt;
            mission.StudySession.DurationSeconds = (int)Math.Clamp((mission.CompletedAt.Value - mission.StudySession.StartedAt).TotalSeconds, 0, 14400);
            mission.StudySession.Score = mission.Score;
        }
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            MissionEntity latest = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
            EnglishMissionTurn? persisted = latest.Turns.FirstOrDefault(item => item.ClientTurnId == clientTurnId);
            if (persisted != null)
            {
                return new EnglishMissionRespondResult
                {
                    Turn = persisted,
                    Mission = latest,
                    TargetWords = latest.TargetWords.OrderBy(word => word.Id).ToList()
                };
            }
            throw new ArgumentException("Mission vừa được cập nhật ở một yêu cầu khác. Vui lòng gửi lại câu trả lời.");
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            MissionEntity latest = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
            EnglishMissionTurn? persisted = latest.Turns.FirstOrDefault(item => item.ClientTurnId == clientTurnId);
            if (persisted != null)
            {
                return new EnglishMissionRespondResult
                {
                    Turn = persisted,
                    Mission = latest,
                    TargetWords = latest.TargetWords.OrderBy(word => word.Id).ToList()
                };
            }
            throw;
        }
        if (mission.Status == "Completed")
        {
            await PublishCompletedAsync(mission, cancellationToken);
        }
        return new EnglishMissionRespondResult { Turn = turn, Mission = mission, TargetWords = mission.TargetWords.OrderBy(word => word.Id).ToList() };
    }

    public async Task CompleteAsync(string userId, int setId, int sessionId, CancellationToken cancellationToken = default)
    {
        MissionEntity mission = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
        if (mission.Status == "Completed") return;
        mission.Status = "Completed";
        mission.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
        mission.Score = CalculateScore(Parse<List<GoalPayload>>(mission.GoalsJson, "[]").Count, 0, mission.TargetWords.Count(word => word.IsUsed), mission.TurnCount);
        mission.StudySession!.CompletedAt = mission.CompletedAt;
        mission.StudySession.DurationSeconds = (int)Math.Clamp((mission.CompletedAt.Value - mission.StudySession.StartedAt).TotalSeconds, 0, 14400);
        mission.StudySession.Score = mission.Score;
        await _context.SaveChangesAsync(cancellationToken);
        await PublishCompletedAsync(mission, cancellationToken);
    }

    private async Task<MissionEntity> GetMissionAsync(string userId, int setId, int sessionId, CancellationToken cancellationToken)
    {
        MissionEntity? mission = await _context.EnglishMissions
            .Include(item => item.TargetWords)
            .Include(item => item.Turns)
            .Include(item => item.StudySession)
            .FirstOrDefaultAsync(item => item.StudySessionId == sessionId, cancellationToken);
        if (mission?.StudySession == null || mission.StudySession.UserId != userId || mission.StudySession.FlashcardSetId != setId)
            throw new UnauthorizedAccessException("Không có quyền truy cập mission này.");
        return mission;
    }

    private static EnglishMissionStartResult ToResult(MissionEntity mission, IReadOnlyList<EnglishMissionTurn> turns) =>
        new() { Mission = mission, TargetWords = mission.TargetWords.OrderBy(word => word.Id).ToList(), Turns = turns };

    private static int CalculateScore(int goalCount, int achievedGoals, int usedWords, int turns) =>
        Math.Clamp((goalCount == 0 ? 0 : achievedGoals * 40 / goalCount) + Math.Min(30, usedWords * 6) + (achievedGoals > 0 ? 20 : 0) + Math.Max(0, 10 - Math.Max(0, turns - 3)), 0, 100);

    private async Task PublishCompletedAsync(MissionEntity mission, CancellationToken cancellationToken)
    {
        await _studyEvents.PublishAsync(new StudySessionCompletedEvent(
            mission.StudySession!.UserId,
            mission.CompletedAt!.Value,
            mission.StudySession.FlashcardSetId,
            mission.StudySessionId,
            StudyMode.EnglishMission,
            mission.Score), cancellationToken);
    }

    private static bool IsValidStartPayload(string content)
    {
        try
        {
            StartPayload payload = Parse<StartPayload>(content, "invalid");
            return !string.IsNullOrWhiteSpace(payload.Title)
                && !string.IsNullOrWhiteSpace(payload.Situation)
                && !string.IsNullOrWhiteSpace(payload.NpcName)
                && !string.IsNullOrWhiteSpace(payload.OpeningLine)
                && payload.Goals?.Any(goal => !string.IsNullOrWhiteSpace(goal.Id)) == true;
        }
        catch (AiProviderUnavailableException) { return false; }
    }

    private static bool IsValidTurnPayload(string content)
    {
        try { return !string.IsNullOrWhiteSpace(Parse<TurnPayload>(content, "invalid").NpcReply); }
        catch (AiProviderUnavailableException) { return false; }
    }

    private static T Parse<T>(string content, string error)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(CleanJson(content));
            T? result = document.RootElement.Deserialize<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? throw new InvalidOperationException();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new AiProviderUnavailableException(error);
        }
    }

    private static string CleanJson(string content)
    {
        string value = content.Trim();
        if (value.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewLine = value.IndexOf('\n');
            int end = value.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && end > firstNewLine) value = value[(firstNewLine + 1)..end];
        }
        return value.Trim();
    }

    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];
    private static string? LimitNullable(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : Limit(value, max);

    private static string BuildStartSystemPrompt() => "Bạn là biên kịch English Mission. Chỉ trả về JSON hợp lệ, không markdown, theo schema: {title:string,situation:string,npcName:string,npcRole:string,openingLine:string,goals:[{id:string,descriptionVi:string}]}. Viết openingLine bằng tiếng Anh, các trường còn lại bằng tiếng Việt trừ title nếu tự nhiên. Không thêm trường khác.";
    private static string BuildTurnSystemPrompt() => "Bạn là gia sư hội thoại tiếng Anh. Chỉ trả JSON hợp lệ theo schema: {npcReply:string,feedbackVi:string,correctionEn:string|null,correctionExplanationVi:string|null,usedTargetWords:string[],achievedGoalIds:string[],missionCompleted:boolean}. npcReply bằng tiếng Anh, phản hồi và giải thích bằng tiếng Việt. Không tự tạo goal hoặc từ không có trong danh sách.";

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
