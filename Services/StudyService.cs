using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services;

// Service xử lý nghiệp vụ học tập
// Quản lý tiến trình học (đã biết/chưa biết) và phiên học
public class StudyService
{
    private readonly AppDbContext _context;

    // Inject AppDbContext
    public StudyService(AppDbContext context)
    {
        _context = context;
    }

    // Lấy danh sách thẻ trong một bộ để học
    public async Task<List<Flashcard>> GetFlashcardsForStudyAsync(int setId, bool starredOnly = false, bool unlearnedOnly = false, string? userId = null)
    {
        var query = _context.Flashcards.Where(f => f.FlashcardSetId == setId);

        if (starredOnly)
        {
            query = query.Where(f => f.IsStarred);
        }

        if (unlearnedOnly && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(f => !_context.UserProgresses.Any(p => p.UserId == userId && p.FlashcardId == f.Id && p.IsLearned));
        }

        return await query.OrderBy(f => f.OrderIndex).ToListAsync();
    }

    public async Task<Dictionary<int, UserProgress>> GetProgressByCardIdAsync(int setId, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return new Dictionary<int, UserProgress>();

        return await _context.UserProgresses
            .Where(p => p.UserId == userId && p.Flashcard != null && p.Flashcard.FlashcardSetId == setId)
            .ToDictionaryAsync(p => p.FlashcardId);
    }

    public async Task<UserStudySettings> GetSettingsAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return new UserStudySettings();

        var settings = await _context.UserStudySettings.FirstOrDefaultAsync(s => s.UserId == userId);
        return settings ?? new UserStudySettings { UserId = userId };
    }

    public async Task<UserStudySettings> SaveSettingsAsync(string userId, UserStudySettings input)
    {
        var settings = await _context.UserStudySettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings == null)
        {
            settings = new UserStudySettings { UserId = userId };
            await _context.UserStudySettings.AddAsync(settings);
        }

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

    // Đánh dấu thẻ đã biết hoặc chưa biết
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

    // Lấy dữ liệu cho Study Hub — trang chọn chế độ học
    public async Task<StudyModeSelectorViewModel> GetStudyModeSelectorDataAsync(int setId, string? userId)
    {
        var set = await _context.FlashcardSets.FindAsync(setId);
        var allCards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == setId)
            .ToListAsync();

        var progresses = string.IsNullOrWhiteSpace(userId)
            ? new Dictionary<int, UserProgress>()
            : await _context.UserProgresses
                .Where(p => p.UserId == userId && allCards.Select(c => c.Id).Contains(p.FlashcardId))
                .ToDictionaryAsync(p => p.FlashcardId);

        var learnedCount = allCards.Count(c => progresses.TryGetValue(c.Id, out var p) && p.IsLearned);
        var starredCount = allCards.Count(c => c.IsStarred);
        var masteryPercent = allCards.Count > 0 ? learnedCount * 100 / allCards.Count : 0;

        var recentCutoff = DateTime.UtcNow.AddDays(-7);
        var recentSessionCount = string.IsNullOrWhiteSpace(userId)
            ? 0
            : await _context.StudySessions.CountAsync(s =>
                s.UserId == userId &&
                s.FlashcardSetId == setId &&
                s.CompletedAt >= recentCutoff);

        var settings = await GetSettingsAsync(userId);
        var filteredCards = await GetFlashcardsForStudyAsync(setId, settings.StarredOnly, settings.UnlearnedOnly, userId);

        var flashcardOption = BuildModeOption(StudyMode.Flashcard, filteredCards, 15, $"/Study/{setId}/Flashcard");
        var dictationCards = filteredCards.Where(c => !string.IsNullOrWhiteSpace(c.ExampleSentence)).ToList();
        var dictationOption = BuildModeOption(StudyMode.Dictation, dictationCards, 25, $"/Study/{setId}/Dictation");

        var modes = new List<StudyModeOptionViewModel> { flashcardOption, dictationOption };
        MarkRecommended(modes, StudyMode.Flashcard);

        var warnings = new List<string>();
        var hasExamples = allCards.Any(c => !string.IsNullOrWhiteSpace(c.ExampleSentence));
        var recommendedMode = DetermineRecommendedMode(masteryPercent, hasExamples);

        if (recommendedMode == StudyMode.Dictation && !dictationOption.IsAvailable)
        {
            recommendedMode = flashcardOption.IsAvailable ? StudyMode.Flashcard : StudyMode.Flashcard;
            warnings.Add("Nghe chép không khả dụng với bộ lọc hiện tại. Đã chuyển sang Flashcard.");
        }

        MarkRecommended(modes, recommendedMode);

        var roadmapModes = new List<StudyModeOptionViewModel>
        {
            new() { Mode = StudyMode.Quiz, Name = "Trắc nghiệm", Description = "Chọn đáp án đúng", IconClass = "ph-question", IsAvailable = false },
            new() { Mode = StudyMode.Write, Name = "Viết chính tả", Description = "Viết lại từ từ gợi ý", IconClass = "ph-pencil-simple", IsAvailable = false },
            new() { Mode = StudyMode.Match, Name = "Ghép đôi", Description = "Ghép từ với nghĩa", IconClass = "ph-shuffle", IsAvailable = false }
        };

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

    private static StudyModeOptionViewModel BuildModeOption(StudyMode mode, List<Flashcard> cards, int secondsPerCard, string actionUrl)
    {
        var (name, description, iconClass) = mode switch
        {
            StudyMode.Flashcard => ("Flashcard", "Lật thẻ và ghi nhớ", "ph-cards"),
            StudyMode.Dictation => ("Nghe chép", "Nghe và viết lại từ", "ph-headphones"),
            StudyMode.Quiz => ("Trắc nghiệm", "Chọn đáp án đúng", "ph-question"),
            StudyMode.Write => ("Viết chính tả", "Viết lại từ từ gợi ý", "ph-pencil-simple"),
            StudyMode.Match => ("Ghép đôi", "Ghép từ với nghĩa", "ph-shuffle"),
            _ => (mode.ToString(), string.Empty, "ph-question")
        };

        return new StudyModeOptionViewModel
        {
            Mode = mode,
            Name = name,
            Description = description,
            IconClass = iconClass,
            ActionUrl = actionUrl,
            IsAvailable = cards.Any(),
            CardCount = cards.Count,
            EstimatedSeconds = cards.Count * secondsPerCard
        };
    }

    private static StudyMode DetermineRecommendedMode(int masteryPercent, bool hasExamples)
    {
        if (masteryPercent >= 50 && hasExamples)
            return StudyMode.Dictation;
        return StudyMode.Flashcard;
    }

    private static void MarkRecommended(List<StudyModeOptionViewModel> modes, StudyMode recommended)
    {
        foreach (var mode in modes)
        {
            mode.IsRecommended = mode.Mode == recommended;
        }
    }
}
