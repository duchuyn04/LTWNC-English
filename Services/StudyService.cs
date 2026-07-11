using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services.StudyModes;

namespace ltwnc.Services;

// Service xử lý nghiệp vụ học tập.
// Trách nhiệm chính:
// - Quản lý settings và tiến trình học (đã biết/chưa biết)
// - Điều phối các IStudyModeStrategy để lấy thẻ và xây dựng Study Hub
// - Không chứa logic lọc thẻ — toàn bộ giao cho strategy
public class StudyService
{
    private readonly AppDbContext _context;
    private readonly IEnumerable<IStudyModeStrategy> _strategies;
    private readonly IStudyModeStrategyResolver _strategyResolver;

    public StudyService(
        AppDbContext context,
        IEnumerable<IStudyModeStrategy> strategies,
        IStudyModeStrategyResolver strategyResolver)
    {
        _context = context;
        _strategies = strategies;
        _strategyResolver = strategyResolver;
    }

    // Lấy danh sách thẻ cho một chế độ học cụ thể.
    // Controller gọi method này thay vì tự query hoặc tự resolve strategy.
    public async Task<List<Flashcard>> GetCardsForModeAsync(
        StudyMode mode,
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        var strategy = _strategyResolver.Resolve(mode);
        return await strategy.GetCardsAsync(setId, settings, userId);
    }

    // Lấy tiến trình học của user cho từng thẻ trong bộ, dùng để hiển thị trạng thái đã biết/chưa biết
    public async Task<Dictionary<int, UserProgress>> GetProgressByCardIdAsync(int setId, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return new Dictionary<int, UserProgress>();

        return await _context.UserProgresses
            .Where(p => p.UserId == userId && p.Flashcard != null && p.Flashcard.FlashcardSetId == setId)
            .ToDictionaryAsync(p => p.FlashcardId);
    }

    // Lấy settings của user; nếu chưa có thì trả về settings mặc định
    public async Task<UserStudySettings> GetSettingsAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return new UserStudySettings();

        var settings = await _context.UserStudySettings.FirstOrDefaultAsync(s => s.UserId == userId);
        return settings ?? new UserStudySettings { UserId = userId };
    }

    // Lưu toàn bộ settings học tập của user
    public async Task<UserStudySettings> SaveSettingsAsync(string userId, UserStudySettings input)
    {
        var settings = await _context.UserStudySettings.FirstOrDefaultAsync(s => s.UserId == userId);
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
        var settings = await GetSettingsAsync(userId);
        if (starredOnly.HasValue) settings.StarredOnly = starredOnly.Value;
        if (unlearnedOnly.HasValue) settings.UnlearnedOnly = unlearnedOnly.Value;
        await SaveSettingsAsync(userId, settings);
    }

    // Đánh dấu một thẻ là đã biết hoặc chưa biết
    public async Task MarkLearnedAsync(string userId, int setId, int flashcardId, bool learned)
    {
        var set = await _context.FlashcardSets.FindAsync(setId);
        if (set == null)
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        if (!set.IsPublic && set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");

        var card = await _context.Flashcards.FindAsync(flashcardId);
        if (card == null || card.FlashcardSetId != setId)
            throw new KeyNotFoundException("Thẻ không tồn tại trong bộ thẻ này.");

        var progress = await _context.UserProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.FlashcardId == flashcardId);

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
        progress.Status = learned ? UserProgressStatus.Mastered : UserProgressStatus.Learning;
        if (learned)
        {
            progress.CorrectCount++;
        }
        else
        {
            progress.WrongCount++;
        }
        progress.LastReviewed = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    // Ghi nhận hoàn thành một phiên học
    public async Task CompleteSessionAsync(string userId, int setId, StudyMode mode)
    {
        var set = await _context.FlashcardSets.FindAsync(setId);
        if (set == null)
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        if (!set.IsPublic && set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");

        var session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = mode,
            CompletedAt = DateTime.UtcNow
        };
        await _context.StudySessions.AddAsync(session);
        await _context.SaveChangesAsync();
    }

    // Lấy dữ liệu cho Study Hub — trang chọn chế độ học.
    // Mỗi strategy tự quyết định thẻ khả dụng và tự xây dựng option hiển thị.
    public async Task<StudyModeSelectorViewModel> GetStudyModeSelectorDataAsync(int setId, string? userId)
    {
        // Thông tin cơ bản của bộ thẻ
        var set = await _context.FlashcardSets.FindAsync(setId);
        var allCards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == setId)
            .ToListAsync();

        // Tiến trình học của user cho các thẻ trong bộ
        var progresses = string.IsNullOrWhiteSpace(userId)
            ? new Dictionary<int, UserProgress>()
            : await _context.UserProgresses
                .Where(p => p.UserId == userId && allCards.Select(c => c.Id).Contains(p.FlashcardId))
                .ToDictionaryAsync(p => p.FlashcardId);

        // Thống kê hiển thị trên Study Hub
        var learnedCount = allCards.Count(c => progresses.TryGetValue(c.Id, out var p) && p.IsLearned);
        var starredCount = allCards.Count(c => c.IsStarred);
        var masteryPercent = allCards.Count > 0 ? learnedCount * 100 / allCards.Count : 0;

        // Số phiên học trong 7 ngày gần nhất
        var recentCutoff = DateTime.UtcNow.AddDays(-7);
        var recentSessionCount = string.IsNullOrWhiteSpace(userId)
            ? 0
            : await _context.StudySessions.CountAsync(s =>
                s.UserId == userId &&
                s.FlashcardSetId == setId &&
                s.CompletedAt >= recentCutoff);

        var settings = await GetSettingsAsync(userId);

        // Xây dựng danh sách mode khả dụng từ các strategy đã đăng ký.
        // Duyệt theo từng mode duy nhất và resolve qua resolver để đảm bảo tính duy nhất.
        var modes = new List<StudyModeOptionViewModel>();
        foreach (var mode in _strategies.Select(s => s.Mode).Distinct().OrderBy(m => (int)m))
        {
            var strategy = _strategyResolver.Resolve(mode);
            var cards = await strategy.GetCardsAsync(setId, settings, userId);
            modes.Add(strategy.BuildOption(setId, cards, settings));
        }

        // Xác định mode được đề xuất dựa trên mastery và khả năng thực tế của Dictation
        var warnings = new List<string>();
        var recommendedMode = DetermineRecommendedMode(masteryPercent, modes);

        // Nếu mode đề xuất không khả dụng, chuyển sang mode khả dụng đầu tiên và cảnh báo user
        if (!modes.Any(m => m.Mode == recommendedMode && m.IsAvailable))
        {
            var fallback = modes.FirstOrDefault(m => m.IsAvailable);
            if (fallback != null)
            {
                var recommendedName = modes.FirstOrDefault(m => m.Mode == recommendedMode)?.Name
                    ?? recommendedMode.ToString();
                warnings.Add($"{recommendedName} không khả dụng với bộ lọc hiện tại. Đã chuyển sang {fallback.Name}.");
                recommendedMode = fallback.Mode;
            }
            else
            {
                warnings.Add("Không có thẻ phù hợp với bộ lọc hiện tại. Hãy điều chỉnh bộ lọc hoặc thêm thẻ mới.");
            }
        }

        MarkRecommended(modes, recommendedMode);

        // Roadmap: chỉ hiển thị các mode chưa có strategy thật đăng ký
        var activeModes = modes.Select(m => m.Mode).ToHashSet();
        var roadmapModes = new[] { StudyMode.Quiz, StudyMode.Write, StudyMode.Match }
            .Where(mode => !activeModes.Contains(mode))
            .Select(BuildRoadmapMode)
            .ToList();

        return new StudyModeSelectorViewModel
        {
            SetId = setId,
            SetTitle = set?.Title ?? string.Empty,
            SetDescription = set?.Description,
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

    // Tạo option cho các mode chưa triển khai ( roadmap )
    private static StudyModeOptionViewModel BuildRoadmapMode(StudyMode mode)
    {
        var metadata = GetModeMetadata(mode);
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
        return mode switch
        {
            StudyMode.Quiz => new ModeMetadata("Trắc nghiệm", "Chọn đáp án đúng", "ph-question", 30),
            StudyMode.Write => new ModeMetadata("Viết chính tả", "Viết lại từ từ gợi ý", "ph-pencil-simple", 30),
            StudyMode.Match => new ModeMetadata("Ghép đôi", "Ghép từ với nghĩa", "ph-shuffle", 30),
            _ => new ModeMetadata(mode.ToString(), string.Empty, "ph-question", 30)
        };
    }

    private sealed record ModeMetadata(string Name, string Description, string IconClass, int SecondsPerCard);

    // Đề xuất Dictation khi user đã thuộc >= 50% thẻ VÀ Dictation đang khả dụng với settings hiện tại
    private static StudyMode DetermineRecommendedMode(
        int masteryPercent,
        IReadOnlyList<StudyModeOptionViewModel> modes)
    {
        var dictationAvailable = modes.Any(m =>
            m.Mode == StudyMode.Dictation && m.IsAvailable);

        return masteryPercent >= 50 && dictationAvailable
            ? StudyMode.Dictation
            : StudyMode.Flashcard;
    }

    // Đánh dấu mode được đề xuất trên danh sách option
    private static void MarkRecommended(List<StudyModeOptionViewModel> modes, StudyMode recommended)
    {
        foreach (var mode in modes)
        {
            mode.IsRecommended = mode.Mode == recommended;
        }
    }
}
