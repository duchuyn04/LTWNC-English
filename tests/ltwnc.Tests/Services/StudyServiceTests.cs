using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services;

public class StudyServiceTests
{
    private AppDbContext CreateContext()
    {
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
    public async Task GetStudyModeSelectorDataAsync_EmptySet_ReturnsZeroStatsAndFlashcardRecommended()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(0, result.TotalCards);
        Assert.Equal(0, result.LearnedCount);
        Assert.Equal(0, result.MasteryPercent);
        Assert.Equal(StudyMode.Flashcard, result.RecommendedMode);
        Assert.Equal(2, result.Modes.Count);
    }

    [Fact]
    public async Task GetStudyModeSelectorDataAsync_AllCardsUnlearned_RecommendsFlashcard()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(StudyMode.Flashcard, result.RecommendedMode);
        var flashcard = result.Modes.Single(m => m.Mode == StudyMode.Flashcard);
        Assert.True(flashcard.IsAvailable);
    }

    [Fact]
    public async Task GetStudyModeSelectorDataAsync_HighMasteryWithExamples_RecommendsDictation()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào", exampleSentence: "Hello, world!");
        await SeedCardAsync(context, 2, "world", "thế giới", exampleSentence: "World peace.");
        await SeedProgressAsync(context, 1, true);
        await SeedProgressAsync(context, 2, true);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(100, result.MasteryPercent);
        Assert.Equal(StudyMode.Dictation, result.RecommendedMode);
        Assert.Equal(2, result.Modes.Count);
        var dictation = result.Modes.Single(m => m.Mode == StudyMode.Dictation);
        Assert.True(dictation.IsAvailable);
        Assert.Equal(2, dictation.CardCount);
    }

    [Fact]
    public async Task GetStudyModeSelectorDataAsync_HighMasteryNoExamples_RecommendsFlashcard()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào", exampleSentence: "");
        await SeedProgressAsync(context, 1, true);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(StudyMode.Flashcard, result.RecommendedMode);
    }

    [Fact]
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
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        var flashcard = result.Modes.Single(m => m.Mode == StudyMode.Flashcard);
        Assert.Equal(1, flashcard.CardCount);
        Assert.True(result.StarredOnly);
    }

    [Fact]
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
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(StudyMode.Flashcard, result.RecommendedMode);
        Assert.Contains(result.Warnings, w => w.Contains("Nghe chép"));
    }

    [Fact]
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
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(1, result.RecentSessionCount);
    }

    [Fact]
    public async Task SaveFilterSettingsAsync_UpdatesStarredAndUnlearnedOnly()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver);
        await service.SaveFilterSettingsAsync("user-1", starredOnly: true, unlearnedOnly: false);

        var settings = await context.UserStudySettings.FirstAsync(s => s.UserId == "user-1");
        Assert.True(settings.StarredOnly);
        Assert.False(settings.UnlearnedOnly);
    }

    [Fact]
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
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, userId: null);

        Assert.Equal(1, result.TotalCards);
        Assert.False(result.StarredOnly);
        Assert.False(result.UnlearnedOnly);
        Assert.Equal(0, result.RecentSessionCount);
    }

    [Fact]
    public async Task GetStudyModeSelectorDataAsync_MasteryPercent_RoundsDown()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "a", "1");
        await SeedCardAsync(context, 2, "b", "2");
        await SeedCardAsync(context, 3, "c", "3");
        await SeedProgressAsync(context, 1, true);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(33, result.MasteryPercent);
    }

    [Fact]
    public async Task GetStudyModeSelectorDataAsync_DictationRecommendedOnlyWhenMasteryAndExamples()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        var card = await SeedCardAsync(context, 1, "hello", "xin chào", exampleSentence: "Hello!");
        await SeedProgressAsync(context, 1, true);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver);

        var withExample = await service.GetStudyModeSelectorDataAsync(1, "user-1");
        Assert.Equal(StudyMode.Dictation, withExample.RecommendedMode);

        card.ExampleSentence = "";
        await context.SaveChangesAsync();

        var withoutExample = await service.GetStudyModeSelectorDataAsync(1, "user-1");
        Assert.Equal(StudyMode.Flashcard, withoutExample.RecommendedMode);
    }

    [Fact]
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
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        var flashcard = result.Modes.Single(m => m.Mode == StudyMode.Flashcard);
        Assert.Equal(1, flashcard.CardCount);
    }

    [Fact]
    public async Task GetStudyModeSelectorDataAsync_RoadmapModes_HaveCorrectUnavailableReason()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);

        var (strategies, resolver) = CreateStrategies(context);
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.All(result.RoadmapModes, m => Assert.Equal("Sắp ra mắt", m.UnavailableReason));
    }

    [Fact]
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
        var service = new StudyService(context, strategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(StudyMode.Flashcard, result.RecommendedMode);
        Assert.Contains(result.Warnings, w => w.Contains("Nghe chép") && w.Contains("Flashcard"));
    }

    [Fact]
    public async Task GetStudyModeSelectorDataAsync_NewStrategy_IsProcessedWithoutHardCodedSwitch()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var (baseStrategies, _) = CreateStrategies(context);
        baseStrategies.Add(new FakeQuizStrategy());
        var resolver = new StudyModeStrategyResolver(baseStrategies);

        var service = new StudyService(context, baseStrategies, resolver);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        var quizOption = result.Modes.SingleOrDefault(m => m.Mode == StudyMode.Quiz);
        Assert.NotNull(quizOption);
        Assert.Equal("Quiz", quizOption.Name);
        Assert.Equal(1, quizOption.CardCount);
    }

    // Strategy giả để kiểm tra khả năng mở rộng mà không cần sửa StudyService
    private sealed class FakeQuizStrategy : IStudyModeStrategy
    {
        public StudyMode Mode => StudyMode.Quiz;

        public Task<List<Flashcard>> GetCardsAsync(int setId, UserStudySettings settings, string? userId)
            => Task.FromResult(new List<Flashcard>());

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
                IsAvailable = true,
                CardCount = 1,
                EstimatedSeconds = 30
            };
        }
    }
}
