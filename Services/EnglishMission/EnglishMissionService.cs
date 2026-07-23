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
        _context = context;
        _studyService = studyService;
        _router = router;
        _studyEvents = studyEvents;
        _timeProvider = timeProvider;
    }

    // Trả danh sách chủ đề cố định để controller không tự tạo nội dung nhiệm vụ.
    public IReadOnlyList<EnglishMissionTopic> GetTopics()
    {
        return Topics;
    }

    // Kiểm tra quyền trên bộ thẻ, lấy từ mục tiêu và tạo Nhiệm vụ tiếng Anh mới bằng AI.
    public async Task<EnglishMissionStartResult> StartAsync(
        string userId,
        int setId,
        string topic,
        CancellationToken cancellationToken = default)
    {
        EnglishMissionTopic selectedTopic = Topics.FirstOrDefault(item => item.Id == topic)
            ?? throw new ArgumentException("Chủ đề không hợp lệ.");

        FlashcardSet? set = await _context.FlashcardSets.FindAsync([setId], cancellationToken);
        EnsureSetAccess(set, userId);

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
            PlannedItemCount = cards.Count,
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
        await SaveWithCurrentSetAccessAsync(userId, setId, cancellationToken);
        return ToResult(mission, []);
    }

    // Tải nhiệm vụ thuộc đúng người học cùng toàn bộ từ mục tiêu và lượt hội thoại đã lưu.
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

    // Gửi một lượt hội thoại, lọc dữ liệu AI và lưu kết quả idempotent theo clientTurnId.
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
            await SaveWithCurrentSetAccessAsync(userId, setId, cancellationToken);
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

    // Kết thúc nhiệm vụ thủ công, cập nhật phiên học và phát sự kiện hoàn thành đúng một lần.
    public async Task CompleteAsync(string userId, int setId, int sessionId, CancellationToken cancellationToken = default)
    {
        MissionEntity mission = await GetMissionAsync(userId, setId, sessionId, cancellationToken);
        if (mission.Status == "Completed") return;
        mission.Status = "Completed";
        mission.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
        List<GoalPayload> goals = Parse<List<GoalPayload>>(mission.GoalsJson, "[]");
        HashSet<string> validGoalIds = goals
            .Select(goal => goal.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        int achievedGoals = mission.Turns
            .SelectMany(turn => Parse<List<string>>(turn.AchievedGoalsJson, "[]"))
            .Where(validGoalIds.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        mission.Score = CalculateScore(
            goals.Count,
            achievedGoals,
            mission.TargetWords.Count(word => word.IsUsed),
            mission.TurnCount);
        mission.StudySession!.CompletedAt = mission.CompletedAt;
        mission.StudySession.DurationSeconds = (int)Math.Clamp((mission.CompletedAt.Value - mission.StudySession.StartedAt).TotalSeconds, 0, 14400);
        mission.StudySession.Score = mission.Score;
        await SaveWithCurrentSetAccessAsync(userId, setId, cancellationToken);
        await PublishCompletedAsync(mission, cancellationToken);
    }

    // Tải nhiệm vụ cùng navigation và xác nhận nó thuộc đúng user, bộ thẻ và phiên học yêu cầu.
    private async Task<MissionEntity> GetMissionAsync(string userId, int setId, int sessionId, CancellationToken cancellationToken)
    {
        MissionEntity? mission = await _context.EnglishMissions
            .Include(item => item.TargetWords)
            .Include(item => item.Turns)
            .Include(item => item.StudySession)
                .ThenInclude(session => session!.FlashcardSet)
            .FirstOrDefaultAsync(item => item.StudySessionId == sessionId, cancellationToken);
        if (mission?.StudySession == null || mission.StudySession.UserId != userId || mission.StudySession.FlashcardSetId != setId)
            throw new UnauthorizedAccessException("Không có quyền truy cập mission này.");

        // Dùng cùng quy tắc với lúc bắt đầu để thay đổi cách ly hoặc quyền riêng tư có hiệu lực ngay.
        EnsureSetAccess(mission.StudySession.FlashcardSet, userId);

        return mission;
    }

    // Kiểm tra lại quyền truy cập trong cùng transaction với lần ghi để không lọt thay đổi kiểm duyệt đồng thời.
    private async Task SaveWithCurrentSetAccessAsync(
        string userId,
        int setId,
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
        {
            try
            {
                await EnsureCurrentSetAccessAsync(userId, setId, cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                _context.ChangeTracker.Clear();
                throw;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
        try
        {
            await EnsureCurrentSetAccessAsync(userId, setId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            throw;
        }
        catch (KeyNotFoundException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // Đọc lại bộ thẻ không tracking để không dùng trạng thái cũ đã được nạp trước khi gọi AI.
    private async Task EnsureCurrentSetAccessAsync(
        string userId,
        int setId,
        CancellationToken cancellationToken)
    {
        FlashcardSet? currentSet = await _context.FlashcardSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == setId, cancellationToken);
        EnsureSetAccess(currentSet, userId);
    }

    // Áp dụng một quy tắc chung: tác giả được truy cập, người khác cần bộ công khai và không bị cách ly.
    private static void EnsureSetAccess(FlashcardSet? set, string userId)
    {
        if (set == null)
        {
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        }

        bool isOwner = string.Equals(set.UserId, userId, StringComparison.Ordinal);
        if (set.ModerationStatus == FlashcardSetModerationStatus.Quarantined && !isOwner)
        {
            throw new UnauthorizedAccessException(
                "Bộ thẻ đang bị cách ly và không thể dùng để học công khai.");
        }

        if (!set.IsPublic && !isOwner)
        {
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");
        }
    }

    // Chuyển entity sang kết quả bắt đầu với thứ tự từ mục tiêu ổn định.
    private static EnglishMissionStartResult ToResult(MissionEntity mission, IReadOnlyList<EnglishMissionTurn> turns) =>
        new() { Mission = mission, TargetWords = mission.TargetWords.OrderBy(word => word.Id).ToList(), Turns = turns };

    // Tính điểm từ mục tiêu, từ đã dùng và số lượt rồi giới hạn trong thang 0 đến 100.
    private static int CalculateScore(int goalCount, int achievedGoals, int usedWords, int turns)
    {
        int goalScore = 0;
        if (goalCount > 0)
        {
            goalScore = achievedGoals * 40 / goalCount;
        }

        int vocabularyScore = Math.Min(30, usedWords * 6);
        int completionBonus = 0;
        if (achievedGoals > 0)
        {
            completionBonus = 20;
        }

        int turnScore = Math.Max(0, 10 - Math.Max(0, turns - 3));
        return Math.Clamp(goalScore + vocabularyScore + completionBonus + turnScore, 0, 100);
    }

    // Phát sự kiện hoàn thành để các observer cập nhật tiến độ và thành tích từ cùng dữ liệu phiên học.
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

    // Kiểm tra payload khởi tạo có đủ các trường bắt buộc trước khi router chấp nhận phản hồi AI.
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

    // Kiểm tra payload lượt hội thoại có câu trả lời NPC hợp lệ.
    private static bool IsValidTurnPayload(string content)
    {
        try { return !string.IsNullOrWhiteSpace(Parse<TurnPayload>(content, "invalid").NpcReply); }
        catch (AiProviderUnavailableException) { return false; }
    }

    // Parse JSON đã làm sạch và chuyển lỗi định dạng thành lỗi AI thống nhất cho tầng trên.
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

    // Bỏ code fence nếu nhà cung cấp bọc JSON trong Markdown.
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

    // Cắt chuỗi theo giới hạn cột nhưng giữ nguyên chuỗi ngắn hơn.
    private static string Limit(string value, int max)
    {
        if (value.Length <= max)
        {
            return value;
        }

        return value[..max];
    }

    // Chuẩn hóa chuỗi tùy chọn trước khi áp dụng giới hạn độ dài.
    private static string? LimitNullable(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Limit(value, max);
    }

    // Tạo system prompt cố định cho bước khởi tạo nhiệm vụ.
    private static string BuildStartSystemPrompt() => "Bạn là biên kịch English Mission. Chỉ trả về JSON hợp lệ, không markdown, theo schema: {title:string,situation:string,npcName:string,npcRole:string,openingLine:string,goals:[{id:string,descriptionVi:string}]}. Viết openingLine bằng tiếng Anh, các trường còn lại bằng tiếng Việt trừ title nếu tự nhiên. Không thêm trường khác.";

    // Tạo system prompt cố định cho từng lượt hội thoại.
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
