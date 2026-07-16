using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services.StudyModes;
using ltwnc.Services.StudyEvents;

namespace ltwnc.Services.Study;

// Nghiệp vụ học: settings, tiến độ thẻ, Study Hub, phát sự kiện Observer.
// Không lọc thẻ trong service (giao strategy). Không tự mở huy hiệu.
public class StudyService : IStudyService
{
    // Progress, settings, session, flashcard set
    private readonly AppDbContext _context;

    // Các strategy đăng ký DI (dùng liệt kê mode trên hub)
    private readonly IEnumerable<IStudyModeStrategy> _strategies;

    // Resolve đúng strategy theo StudyMode
    private readonly IStudyModeStrategyResolver _strategyResolver;

    // Subject Observer: publish sau khi Save progress / session
    private readonly IStudyEventPublisher _studyEvents;

    // Inject DbContext, strategy, resolver, publisher
    public StudyService(
        AppDbContext context,
        IEnumerable<IStudyModeStrategy> strategies,
        IStudyModeStrategyResolver strategyResolver,
        IStudyEventPublisher studyEvents)
    {
        _context = context;
        _strategies = strategies;
        _strategyResolver = strategyResolver;
        _studyEvents = studyEvents;
    }

    // Lấy danh sách thẻ cho một chế độ học cụ thể.
    // Controller gọi method này thay vì tự query hoặc tự resolve strategy.
    public async Task<List<Flashcard>> GetCardsForModeAsync(
        StudyMode mode,
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        IStudyModeStrategy strategy = _strategyResolver.Resolve(mode);
        List<Flashcard> cards = await strategy.GetCardsAsync(setId, settings, userId);
        return cards;
    }

    // Lấy tiến trình học của user cho từng thẻ trong bộ, dùng để hiển thị trạng thái đã biết/chưa biết
    public async Task<Dictionary<int, UserProgress>> GetProgressByCardIdAsync(int setId, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new Dictionary<int, UserProgress>();
        }

        Dictionary<int, UserProgress> progressByCardId = await _context.UserProgresses
            .Where(progress =>
                progress.UserId == userId
                && progress.Flashcard != null
                && progress.Flashcard.FlashcardSetId == setId)
            .ToDictionaryAsync(progress => progress.FlashcardId);

        return progressByCardId;
    }

    // Lấy settings của user; nếu chưa có thì trả về settings mặc định
    public async Task<UserStudySettings> GetSettingsAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new UserStudySettings();
        }

        UserStudySettings? settings = await _context.UserStudySettings
            .FirstOrDefaultAsync(row => row.UserId == userId);

        if (settings == null)
        {
            return new UserStudySettings { UserId = userId };
        }

        return settings;
    }

    // Lưu toàn bộ settings học tập của user
    public async Task<UserStudySettings> SaveSettingsAsync(string userId, UserStudySettings input)
    {
        UserStudySettings? settings = await _context.UserStudySettings
            .FirstOrDefaultAsync(row => row.UserId == userId);

        if (settings == null)
        {
            settings = new UserStudySettings { UserId = userId };
            await _context.UserStudySettings.AddAsync(settings);
        }

        // Cập nhật bộ lọc và cài đặt hiển thị flashcard
        settings.StarredOnly = input.StarredOnly;
        settings.UnlearnedOnly = input.UnlearnedOnly;
        settings.ShowFrontTerm = input.ShowFrontTerm;
        settings.ShowFrontDefinition = input.ShowFrontDefinition;
        settings.ShowFrontIpa = input.ShowFrontIpa;
        settings.ShowFrontImage = input.ShowFrontImage;
        settings.ShowBackTerm = input.ShowBackTerm;
        settings.ShowBackDefinition = input.ShowBackDefinition;
        settings.ShowBackIpa = input.ShowBackIpa;
        settings.ShowBackExample = input.ShowBackExample;
        settings.ShowBackImage = input.ShowBackImage;
        settings.HideImage = input.HideImage;
        settings.BlurImage = input.BlurImage;
        settings.LargeImage = input.LargeImage;
        settings.PronounceFront = input.PronounceFront;
        settings.PronounceBack = input.PronounceBack;

        // Cập nhật cài đặt riêng của Dictation
        settings.DictationContentMode = input.DictationContentMode;
        settings.DictationAnswerMode = input.DictationAnswerMode;
        settings.DictationAutoAdvance = input.DictationAutoAdvance;
        settings.DictationPlaybackSpeed = input.DictationPlaybackSpeed;
        settings.DictationVoiceUri = input.DictationVoiceUri;
        settings.DictationShowHint = input.DictationShowHint;
        settings.DictationAcceptSynonyms = input.DictationAcceptSynonyms;
        settings.DictationShuffle = input.DictationShuffle;

        await _context.SaveChangesAsync();
        return settings;
    }

    // Cập nhật nhanh hai bộ lọc StarredOnly/UnlearnedOnly từ query string trên URL
    public async Task SaveFilterSettingsAsync(string userId, bool? starredOnly, bool? unlearnedOnly)
    {
        UserStudySettings settings = await GetSettingsAsync(userId);

        if (starredOnly.HasValue)
        {
            settings.StarredOnly = starredOnly.Value;
        }

        if (unlearnedOnly.HasValue)
        {
            settings.UnlearnedOnly = unlearnedOnly.Value;
        }

        await SaveSettingsAsync(userId, settings);
    }

    // Đánh dấu một thẻ là đã biết hoặc chưa biết
    public async Task MarkLearnedAsync(string userId, int setId, int flashcardId, bool learned)
    {
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);
        if (set == null)
        {
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        }

        if (!set.IsPublic && set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");
        }

        Flashcard? card = await _context.Flashcards.FindAsync(flashcardId);
        if (card == null || card.FlashcardSetId != setId)
        {
            throw new KeyNotFoundException("Thẻ không tồn tại trong bộ thẻ này.");
        }

        UserProgress? progress = await _context.UserProgresses
            .FirstOrDefaultAsync(row => row.UserId == userId && row.FlashcardId == flashcardId);

        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId,
                FlashcardId = flashcardId
            };
            await _context.UserProgresses.AddAsync(progress);
        }

        progress.IsLearned = learned;

        if (learned)
        {
            progress.Status = UserProgressStatus.Mastered;
            progress.CorrectCount++;
        }
        else
        {
            progress.Status = UserProgressStatus.Learning;
            progress.WrongCount++;
        }

        progress.LastReviewed = DateTime.UtcNow;

        // Lưu tiến độ xong trước; observer đọc DB sẽ thấy dữ liệu mới
        await _context.SaveChangesAsync();

        // Báo cho tất cả "người theo dõi" biết user vừa cập nhật một thẻ
        // (ví dụ: mở huy hiệu "thẻ đầu tiên đã thuộc", ghi log hệ thống)
        await _studyEvents.PublishAsync(new CardProgressChangedEvent(
            UserId: userId,
            OccurredAtUtc: DateTime.UtcNow,
            SetId: setId,
            FlashcardId: flashcardId,
            IsLearned: learned,
            Status: progress.Status));
    }

    // Ghi nhận hoàn thành một phiên học
    public async Task CompleteSessionAsync(string userId, int setId, StudyMode mode)
    {
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);
        if (set == null)
        {
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        }

        if (!set.IsPublic && set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");
        }

        StudySession session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = mode,
            CompletedAt = DateTime.UtcNow
        };

        await _context.StudySessions.AddAsync(session);
        await _context.SaveChangesAsync();

        // Báo buổi học đã xong; observer có thể mở huy hiệu "buổi Flashcard đầu tiên"...
        await _studyEvents.PublishAsync(new StudySessionCompletedEvent(
            UserId: userId,
            OccurredAtUtc: DateTime.UtcNow,
            SetId: setId,
            SessionId: session.Id,
            Mode: mode,
            Score: session.Score));
    }

    // Lấy dữ liệu cho Study Hub (trang chọn chế độ học).
    // Mỗi strategy tự quyết định thẻ khả dụng và tự xây dựng option hiển thị.
    public async Task<StudyModeSelectorViewModel> GetStudyModeSelectorDataAsync(int setId, string? userId)
    {
        // Thông tin cơ bản của bộ thẻ
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);

        List<Flashcard> allCards = await _context.Flashcards
            .Where(flashcard => flashcard.FlashcardSetId == setId)
            .ToListAsync();

        // Tiến trình học của user cho các thẻ trong bộ
        Dictionary<int, UserProgress> progresses;
        if (string.IsNullOrWhiteSpace(userId))
        {
            progresses = new Dictionary<int, UserProgress>();
        }
        else
        {
            List<int> cardIds = allCards.Select(flashcard => flashcard.Id).ToList();

            progresses = await _context.UserProgresses
                .Where(progress =>
                    progress.UserId == userId
                    && cardIds.Contains(progress.FlashcardId))
                .ToDictionaryAsync(progress => progress.FlashcardId);
        }

        // Thống kê hiển thị trên Study Hub
        int learnedCount = 0;
        int starredCount = 0;

        foreach (Flashcard card in allCards)
        {
            if (progresses.TryGetValue(card.Id, out UserProgress? progress) && progress.IsLearned)
            {
                learnedCount++;
            }

            if (card.IsStarred)
            {
                starredCount++;
            }
        }

        int masteryPercent = 0;
        if (allCards.Count > 0)
        {
            masteryPercent = learnedCount * 100 / allCards.Count;
        }

        // Số phiên học trong 7 ngày gần nhất
        DateTime recentCutoff = DateTime.UtcNow.AddDays(-7);
        int recentSessionCount = 0;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            recentSessionCount = await _context.StudySessions.CountAsync(session =>
                session.UserId == userId
                && session.FlashcardSetId == setId
                && session.CompletedAt >= recentCutoff);
        }

        UserStudySettings settings = await GetSettingsAsync(userId);

        // Xây dựng danh sách mode khả dụng từ các strategy đã đăng ký.
        // Duyệt theo từng mode duy nhất và resolve qua resolver để đảm bảo tính duy nhất.
        List<StudyMode> registeredModes = _strategies
            .Select(strategy => strategy.Mode)
            .Distinct()
            .OrderBy(mode => (int)mode)
            .ToList();

        List<StudyModeOptionViewModel> modes = new();
        foreach (StudyMode mode in registeredModes)
        {
            IStudyModeStrategy strategy = _strategyResolver.Resolve(mode);
            List<Flashcard> cardsForMode = await strategy.GetCardsAsync(setId, settings, userId);
            StudyModeOptionViewModel option = await strategy.BuildOptionAsync(
                setId,
                cardsForMode,
                settings,
                userId);
            modes.Add(option);
        }

        // Xác định mode được đề xuất dựa trên mastery và khả năng thực tế của Dictation
        List<string> warnings = new();
        StudyMode recommendedMode = DetermineRecommendedMode(masteryPercent, modes);

        // Nếu mode đề xuất không khả dụng, chuyển sang mode khả dụng đầu tiên và cảnh báo user
        bool recommendedIsAvailable = modes.Any(option =>
            option.Mode == recommendedMode && option.IsAvailable);

        if (!recommendedIsAvailable)
        {
            StudyModeOptionViewModel? fallback = modes.FirstOrDefault(option => option.IsAvailable);

            if (fallback != null)
            {
                StudyModeOptionViewModel? recommendedOption =
                    modes.FirstOrDefault(option => option.Mode == recommendedMode);

                string recommendedName;
                if (recommendedOption != null)
                {
                    recommendedName = recommendedOption.Name;
                }
                else
                {
                    recommendedName = recommendedMode.ToString();
                }

                warnings.Add(
                    $"{recommendedName} không khả dụng với bộ lọc hiện tại. Đã chuyển sang {fallback.Name}.");
                recommendedMode = fallback.Mode;
            }
            else
            {
                warnings.Add(
                    "Không có thẻ phù hợp với bộ lọc hiện tại. Hãy điều chỉnh bộ lọc hoặc thêm thẻ mới.");
            }
        }

        MarkRecommended(modes, recommendedMode);

        // Roadmap: chỉ hiển thị các mode chưa có strategy thật đăng ký
        HashSet<StudyMode> activeModes = modes.Select(option => option.Mode).ToHashSet();

        StudyMode[] plannedRoadmapModes =
        {
            StudyMode.Quiz,
            StudyMode.Write,
            StudyMode.Match
        };

        List<StudyModeOptionViewModel> roadmapModes = new();
        foreach (StudyMode plannedMode in plannedRoadmapModes)
        {
            if (!activeModes.Contains(plannedMode))
            {
                roadmapModes.Add(BuildRoadmapMode(plannedMode));
            }
        }

        string setTitle = string.Empty;
        string? setDescription = null;
        if (set != null)
        {
            setTitle = set.Title;
            setDescription = set.Description;
        }

        return new StudyModeSelectorViewModel
        {
            SetId = setId,
            SetTitle = setTitle,
            SetDescription = setDescription,
            TotalCards = allCards.Count,
            LearnedCount = learnedCount,
            StarredCount = starredCount,
            MasteryPercent = masteryPercent,
            RecentSessionCount = recentSessionCount,
            StarredOnly = settings.StarredOnly,
            UnlearnedOnly = settings.UnlearnedOnly,
            RecommendedMode = recommendedMode,
            Modes = modes,
            RoadmapModes = roadmapModes,
            Warnings = warnings
        };
    }

    // Tạo option cho các mode chưa triển khai (roadmap)
    private static StudyModeOptionViewModel BuildRoadmapMode(StudyMode mode)
    {
        ModeMetadata metadata = GetModeMetadata(mode);

        return new StudyModeOptionViewModel
        {
            Mode = mode,
            Name = metadata.Name,
            Description = metadata.Description,
            IconClass = metadata.IconClass,
            IsAvailable = false,
            UnavailableReason = "Sắp ra mắt"
        };
    }

    // Metadata dự phòng cho các mode chưa có strategy thật (roadmap)
    private static ModeMetadata GetModeMetadata(StudyMode mode)
    {
        switch (mode)
        {
            case StudyMode.Quiz:
                return new ModeMetadata("Trắc nghiệm", "Chọn đáp án đúng", "ph-question", 30);
            case StudyMode.Write:
                return new ModeMetadata("Viết chính tả", "Viết lại từ từ gợi ý", "ph-pencil-simple", 30);
            case StudyMode.Match:
                return new ModeMetadata("Ghép đôi", "Ghép từ với nghĩa", "ph-shuffle", 30);
            default:
                return new ModeMetadata(mode.ToString(), string.Empty, "ph-question", 30);
        }
    }

    // Metadata tạm cho mode roadmap (chưa có strategy thật)
    private sealed record ModeMetadata(string Name, string Description, string IconClass, int SecondsPerCard);

    // Đề xuất Dictation khi user đã thuộc >= 50% thẻ VÀ Dictation đang khả dụng với settings hiện tại
    private static StudyMode DetermineRecommendedMode(
        int masteryPercent,
        IReadOnlyList<StudyModeOptionViewModel> modes)
    {
        bool dictationAvailable = modes.Any(option =>
            option.Mode == StudyMode.Dictation && option.IsAvailable);

        if (masteryPercent >= 50 && dictationAvailable)
        {
            return StudyMode.Dictation;
        }

        return StudyMode.Flashcard;
    }

    // Đánh dấu mode được đề xuất trên danh sách option
    private static void MarkRecommended(List<StudyModeOptionViewModel> modes, StudyMode recommended)
    {
        foreach (StudyModeOptionViewModel option in modes)
        {
            option.IsRecommended = option.Mode == recommended;
        }
    }
}
