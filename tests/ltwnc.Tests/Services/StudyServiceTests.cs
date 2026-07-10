using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services;
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

        var service = new StudyService(context);
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

        var service = new StudyService(context);
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

        var service = new StudyService(context);
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

        var service = new StudyService(context);
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

        var service = new StudyService(context);
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
            StarredOnly = true
        });
        await context.SaveChangesAsync();

        var service = new StudyService(context);
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

        var service = new StudyService(context);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Equal(1, result.RecentSessionCount);
    }

    [Fact]
    public async Task SaveFilterSettingsAsync_UpdatesStarredAndUnlearnedOnly()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);

        var service = new StudyService(context);
        await service.SaveFilterSettingsAsync("user-1", starredOnly: true, unlearnedOnly: false);

        var settings = await context.UserStudySettings.FirstAsync(s => s.UserId == "user-1");
        Assert.True(settings.StarredOnly);
        Assert.False(settings.UnlearnedOnly);
    }

    [Fact]
    public async Task GetStudyModeSelectorDataAsync_NoAvailableModes_AddsEmptyStateWarning()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào", isStarred: false);

        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            StarredOnly = true
        });
        await context.SaveChangesAsync();

        var service = new StudyService(context);
        var result = await service.GetStudyModeSelectorDataAsync(1, "user-1");

        Assert.Contains(result.Warnings, w => w.Contains("Không có thẻ phù hợp"));
    }
}
