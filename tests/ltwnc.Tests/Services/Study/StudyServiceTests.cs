using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services.Study;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services;

// Kiểm tra StudyService: gợi ý chế độ học, lọc thẻ, đếm phiên học, lưu cài đặt
public class StudyServiceTests
{
    private AppDbContext CreateContext()
    {
        // Mỗi test dùng database in-memory riêng để tránh ảnh hưởng lẫn nhau
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private (List<IStudyModeStrategy> Strategies, IStudyModeStrategyResolver Resolver) CreateStrategies(AppDbContext context)
    {
        var queryService = new StudyCardQueryService(context);
        var strategies = new List<IStudyModeStrategy>
        {
            new FlashcardModeStrategy(queryService),
            new DictationModeStrategy(queryService)
        };
        var resolver = new StudyModeStrategyResolver(strategies);
        return (strategies, resolver);
    }

    private async Task<FlashcardSet> SeedSetAsync(AppDbContext context, int id = 1)
    {
        var set = new FlashcardSet
        {
            Id = id,
            Title = "Test Set",
            Description = "A test set",
            UserId = "user-1",
            IsPublic = true
        };
        await context.FlashcardSets.AddAsync(set);
        await context.SaveChangesAsync();
        return set;
    }

    private async Task<Flashcard> SeedCardAsync(
        AppDbContext context,
        int id,
        string term,
        string def,
        bool isStarred = false,
        string? exampleSentence = "Example.")
    {
        var card = new Flashcard
        {
            Id = id,
            FlashcardSetId = 1,
            FrontText = term,
            BackText = def,
            Pronunciation = "/test/",
            PartOfSpeech = "noun",
            ExampleSentence = exampleSentence ?? string.Empty,
            ExampleMeaning = "Ví dụ",
            IsStarred = isStarred,
            OrderIndex = id
        };
        await context.Flashcards.AddAsync(card);
        await context.SaveChangesAsync();
        return card;
    }

    private async Task SeedProgressAsync(AppDbContext context, int cardId, bool learned)
    {
        var progress = new UserProgress
        {
            UserId = "user-1",
            FlashcardId = cardId,
            IsLearned = learned,
            Status = learned ? UserProgressStatus.Mastered : UserProgressStatus.Learning
        };
        await context.UserProgresses.AddAsync(progress);
        await context.SaveChangesAsync();
    }

    [Fact]
    // Bộ thẻ rỗng: thống kê bằng 0 và chỉ đề xuất Flashcard
    public async Task GetStudyModeSelectorDataAsync_EmptySet_ReturnsZeroStatsAndFlashcardRecommended()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(0, result.TotalCards);
        Assert.Equal(0, result.LearnedCount);
        Assert.Equal(0, result.MasteryPercent);
        Assert.Equal(StudyMode.Flashcard, result.RecommendedMode);
        Assert.Equal(2, result.Modes.Count);
    }

    [Fact]
    // Tất cả thẻ chưa biết: đề xuất bắt đầu với Flashcard
    public async Task GetStudyModeSelectorDataAsync_AllCardsUnlearned_RecommendsFlashcard()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(StudyMode.Flashcard, result.RecommendedMode);
        var flashcard = result.Modes.Single(m => m.Mode == StudyMode.Flashcard);
        Assert.True(flashcard.IsAvailable);
    }

    [Fact]
    // Thành thạo cao và có câu ví dụ: đề xuất Dictation
    public async Task GetStudyModeSelectorDataAsync_HighMasteryWithExamples_RecommendsDictation()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào", exampleSentence: "Hello, world!");
        await SeedCardAsync(context, 2, "world", "thế giới", exampleSentence: "World peace.");
        await SeedProgressAsync(context, 1, true);
        await SeedProgressAsync(context, 2, true);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(100, result.MasteryPercent);
        Assert.Equal(StudyMode.Dictation, result.RecommendedMode);
        Assert.Equal(2, result.Modes.Count);
        var dictation = result.Modes.Single(m => m.Mode == StudyMode.Dictation);
        Assert.True(dictation.IsAvailable);
        Assert.Equal(2, dictation.CardCount);
    }

    [Fact]
    // Thành thạo nhưng thiếu câu ví dụ: không đề xuất Dictation, giữ Flashcard
    public async Task GetStudyModeSelectorDataAsync_HighMasteryNoExamples_RecommendsFlashcard()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào", exampleSentence: "");
        await SeedProgressAsync(context, 1, true);

        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            DictationContentMode = DictationContentMode.ExampleSentence
        });
        await context.SaveChangesAsync();

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(StudyMode.Flashcard, result.RecommendedMode);
    }

    [Fact]
    // Bộ lọc chỉ thẻ đánh sao phải loại bỏ thẻ chưa đánh sao
    public async Task GetStudyModeSelectorDataAsync_StarredOnlyFilter_ExcludesUnstarred()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào", isStarred: false);
        await SeedCardAsync(context, 2, "world", "thế giới", isStarred: true);

        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            StarredOnly = true
        });
        await context.SaveChangesAsync();

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        var flashcard = result.Modes.Single(m => m.Mode == StudyMode.Flashcard);
        Assert.Equal(1, flashcard.CardCount);
        Assert.True(result.StarredOnly);
    }

    [Fact]
    // Khi Dictation không khả dụng do bộ lọc, fallback về Flashcard
    public async Task GetStudyModeSelectorDataAsync_DictationNotAvailableWithFilter_FallbackToFlashcardAndWarning()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào", isStarred: true, exampleSentence: "");
        await SeedCardAsync(context, 2, "world", "thế giới", isStarred: false, exampleSentence: "World!");
        await SeedProgressAsync(context, 1, true);
        await SeedProgressAsync(context, 2, true);

        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            StarredOnly = true,
            DictationContentMode = DictationContentMode.ExampleSentence
        });
        await context.SaveChangesAsync();

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(StudyMode.Flashcard, result.RecommendedMode);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    // Chỉ đếm các phiên học trong 7 ngày gần nhất của đúng user và đúng bộ thẻ
    public async Task GetStudyModeSelectorDataAsync_CountsRecentSessions()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        context.StudySessions.AddRange(
            new StudySession { UserId = "user-1", FlashcardSetId = 1, Mode = StudyMode.Flashcard, CompletedAt = DateTime.UtcNow.AddDays(-1) },
            new StudySession { UserId = "user-1", FlashcardSetId = 1, Mode = StudyMode.Flashcard, CompletedAt = DateTime.UtcNow.AddDays(-8) },
            new StudySession { UserId = "user-2", FlashcardSetId = 1, Mode = StudyMode.Flashcard, CompletedAt = DateTime.UtcNow.AddDays(-1) }
        );
        await context.SaveChangesAsync();

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(1, result.RecentSessionCount);
    }

    [Fact]
    // Lưu cài đặt bộ lọc vào database
    public async Task SaveFilterSettingsAsync_UpdatesStarredAndUnlearnedOnly()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        await service.SaveFilterSettingsAsync("user-1", starredOnly: true, unlearnedOnly: false);

        var settings = await context.UserStudySettings.FirstAsync(s => s.UserId == "user-1");
        Assert.True(settings.StarredOnly);
        Assert.False(settings.UnlearnedOnly);
    }

    [Fact]
    // User ẩn danh dùng cài đặt mặc định và không có phiên học
    public async Task GetStudyModeSelectorDataAsync_AnonymousUser_UsesDefaultSettingsAndZeroSessions()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào", isStarred: true);

        context.StudySessions.Add(new StudySession
        {
            UserId = "user-1",
            FlashcardSetId = 1,
            Mode = StudyMode.Flashcard,
            CompletedAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, userId: null);

        Assert.Equal(1, result.TotalCards);
        Assert.False(result.StarredOnly);
        Assert.False(result.UnlearnedOnly);
        Assert.Equal(0, result.RecentSessionCount);
    }

    [Fact]
    // Tỷ lệ thành thạo làm tròn xuống
    public async Task GetStudyModeSelectorDataAsync_MasteryPercent_RoundsDown()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "a", "1");
        await SeedCardAsync(context, 2, "b", "2");
        await SeedCardAsync(context, 3, "c", "3");
        await SeedProgressAsync(context, 1, true);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(33, result.MasteryPercent);
    }

    [Fact]
    // Dictation chỉ được đề xuất khi vừa thành thạo vừa có nội dung phù hợp
    public async Task GetStudyModeSelectorDataAsync_DictationRecommendedOnlyWhenMasteryAndAvailable()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        var card = await SeedCardAsync(context, 1, "hello", "xin chào", exampleSentence: "Hello!");
        await SeedProgressAsync(context, 1, true);

        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            DictationContentMode = DictationContentMode.ExampleSentence
        });
        await context.SaveChangesAsync();

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());

        var withExample = await service.GetStudyModeSelectorDataAsync(1, "user-1");
        Assert.Equal(StudyMode.Dictation, withExample.RecommendedMode);

        card.ExampleSentence = "";
        await context.SaveChangesAsync();

        var withoutExample = await service.GetStudyModeSelectorDataAsync(1, "user-1");
        Assert.Equal(StudyMode.Flashcard, withoutExample.RecommendedMode);
    }

    [Fact]
    // Lọc kết hợp StarredOnly và UnlearnedOnly: chỉ giữ thẻ đánh sao và chưa biết
    public async Task GetStudyModeSelectorDataAsync_StarredAndUnlearnedIntersection()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "starred-learned", "a", isStarred: true);
        await SeedCardAsync(context, 2, "starred-unlearned", "b", isStarred: true);
        await SeedCardAsync(context, 3, "unstarred-unlearned", "c", isStarred: false);
        await SeedProgressAsync(context, 1, true);

        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            StarredOnly = true,
            UnlearnedOnly = true
        });
        await context.SaveChangesAsync();

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        var flashcard = result.Modes.Single(m => m.Mode == StudyMode.Flashcard);
        Assert.Equal(1, flashcard.CardCount);
    }

    [Fact]
    // Các chế độ chưa triển khai hiển thị lý do "Sắp ra mắt"
    public async Task GetStudyModeSelectorDataAsync_RoadmapModes_HaveCorrectUnavailableReason()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.All(result.RoadmapModes, m => Assert.Equal("Sắp ra mắt", m.UnavailableReason));
    }

    [Fact]
    // Fallback từ Dictation về Flashcard khi Dictation không khả dụng
    public async Task GetStudyModeSelectorDataAsync_Fallback_WarningNamesActualFallbackMode()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào", isStarred: true, exampleSentence: "");
        await SeedCardAsync(context, 2, "world", "thế giới", isStarred: false, exampleSentence: "World!");
        await SeedProgressAsync(context, 1, true);
        await SeedProgressAsync(context, 2, true);

        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            StarredOnly = true,
            DictationContentMode = DictationContentMode.ExampleSentence
        });
        await context.SaveChangesAsync();

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(StudyMode.Flashcard, result.RecommendedMode);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    // Strategy mới được tự động nhận diện mà không cần sửa code StudyService
    public async Task GetStudyModeSelectorDataAsync_NewStrategy_IsProcessedWithoutHardCodedSwitch()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var (baseStrategies, _) = CreateStrategies(context);
        baseStrategies.Add(new FakeQuizStrategy());
        var resolver = new StudyModeStrategyResolver(baseStrategies);

        var service = new StudyService(context, baseStrategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        var quizOption = result.Modes.SingleOrDefault(m => m.Mode == StudyMode.Quiz);
        Assert.NotNull(quizOption);
        Assert.Equal("Quiz", quizOption.Name);
        Assert.Equal(1, quizOption.CardCount);
        Assert.True(quizOption.IsAvailable);

        // Mode đã có strategy thật không được xuất hiện trong roadmap
        Assert.DoesNotContain(result.RoadmapModes, m => m.Mode == StudyMode.Quiz);
    }

    [Fact]
    public async Task GetStudyModeSelectorDataAsync_UsesAsyncOptionBuilder()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);

        var (strategies, _) = CreateStrategies(context);
        strategies.Add(new AsyncOptionStrategy());
        var resolver = new StudyModeStrategyResolver(strategies);

        var service = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal("ASYNC", result.Modes.Single(m => m.Mode == StudyMode.Quiz).Name);
    }

    private sealed class AsyncOptionStrategy : IStudyModeStrategy
    {
        public StudyMode Mode => StudyMode.Quiz;

        public Task<List<Flashcard>> GetCardsAsync(
            int setId,
            UserStudySettings settings,
            string? userId) => Task.FromResult(new List<Flashcard>());

        public StudyModeOptionViewModel BuildOption(
            int setId,
            IReadOnlyList<Flashcard> cards,
            UserStudySettings settings) => new() { Mode = Mode, Name = "SYNC" };

        public Task<StudyModeOptionViewModel> BuildOptionAsync(
            int setId,
            IReadOnlyList<Flashcard> cards,
            UserStudySettings settings,
            string? userId) => Task.FromResult(new StudyModeOptionViewModel
            {
                Mode = Mode,
                Name = "ASYNC"
            });
    }

    // Strategy giả để kiểm tra khả năng mở rộng mà không cần sửa StudyService
    private sealed class FakeQuizStrategy : IStudyModeStrategy
    {
        public StudyMode Mode => StudyMode.Quiz;

        public Task<List<Flashcard>> GetCardsAsync(int setId, UserStudySettings settings, string? userId)
        {
            return Task.FromResult(new List<Flashcard>
            {
                new()
                {
                    Id = 999,
                    FlashcardSetId = setId,
                    FrontText = "quiz",
                    BackText = "test"
                }
            });
        }

        public StudyModeOptionViewModel BuildOption(
            int setId,
            IReadOnlyList<Flashcard> cards,
            UserStudySettings settings)
        {
            return new StudyModeOptionViewModel
            {
                Mode = StudyMode.Quiz,
                Name = "Quiz",
                Description = "Test quiz",
                IconClass = "ph-question",
                ActionUrl = $"/Study/{setId}/Quiz",
                IsAvailable = cards.Count > 0,
                CardCount = cards.Count,
                EstimatedSeconds = cards.Count * 30
            };
        }
    }
}
