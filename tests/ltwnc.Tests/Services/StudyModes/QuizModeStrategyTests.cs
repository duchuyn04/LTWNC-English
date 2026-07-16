using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Study;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.StudyModes;

public class QuizModeStrategyTests
{
    [Fact]
    public async Task GetCardsAsync_applies_study_filters()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetsAsync(context);
        context.Flashcards.AddRange(
            CreateCard(1, 1, "learned starred", "one", orderIndex: 2, isStarred: true),
            CreateCard(2, 1, "unlearned starred", "two", orderIndex: 1, isStarred: true),
            CreateCard(3, 1, "unlearned unstarred", "three", orderIndex: 0),
            CreateCard(4, 2, "other set", "four", orderIndex: 0, isStarred: true));
        context.UserProgresses.Add(new UserProgress
        {
            UserId = "user-1",
            FlashcardId = 1,
            IsLearned = true,
            Status = UserProgressStatus.Mastered
        });
        await context.SaveChangesAsync();
        QuizModeStrategy strategy = CreateStrategy(context);

        List<Flashcard> cards = await strategy.GetCardsAsync(
            1,
            new UserStudySettings { StarredOnly = true, UnlearnedOnly = true },
            "user-1");

        Flashcard card = Assert.Single(cards);
        Assert.Equal(2, card.Id);
    }

    [Fact]
    public async Task BuildOptionAsync_is_available_with_valid_pool()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedValidPoolAsync(context);
        QuizModeStrategy strategy = CreateStrategy(context);

        var option = await strategy.BuildOptionAsync(
            1,
            cards,
            new UserStudySettings(),
            "user-1");

        Assert.Equal(StudyMode.Quiz, option.Mode);
        Assert.Equal("Trắc nghiệm", option.Name);
        Assert.Equal("Chọn đáp án đúng", option.Description);
        Assert.Equal("/Study/1/Quiz", option.ActionUrl);
        Assert.Equal("ph-question", option.IconClass);
        Assert.Equal(cards.Count, option.CardCount);
        Assert.Equal(cards.Count * 30, option.EstimatedSeconds);
        Assert.True(option.IsAvailable);
        Assert.Null(option.UnavailableReason);
        Assert.False(option.IsRecommended);
    }

    [Fact]
    public async Task BuildOptionAsync_explains_empty_filtered_questions()
    {
        await using AppDbContext context = CreateContext();
        await SeedValidPoolAsync(context);
        QuizModeStrategy strategy = CreateStrategy(context);

        var option = await strategy.BuildOptionAsync(
            1,
            Array.Empty<Flashcard>(),
            new UserStudySettings(),
            "user-1");

        Assert.False(option.IsAvailable);
        Assert.Equal(0, option.CardCount);
        Assert.Equal(0, option.EstimatedSeconds);
        Assert.Equal("Không có thẻ phù hợp với bộ lọc hiện tại.", option.UnavailableReason);
    }

    [Fact]
    public async Task BuildOptionAsync_explains_insufficient_library_pool()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetsAsync(context);
        Flashcard card = CreateCard(1, 1, "only term", "only definition");
        context.Flashcards.Add(card);
        await context.SaveChangesAsync();
        QuizModeStrategy strategy = CreateStrategy(context);

        var option = await strategy.BuildOptionAsync(
            1,
            new[] { card },
            new UserStudySettings(),
            "user-1");

        Assert.False(option.IsAvailable);
        Assert.Equal(QuizQuestionFactory.InsufficientPoolReason, option.UnavailableReason);
    }

    private static QuizModeStrategy CreateStrategy(AppDbContext context)
    {
        return new QuizModeStrategy(
            new StudyCardQueryService(context),
            new QuizQuestionFactory(context));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<List<Flashcard>> SeedValidPoolAsync(AppDbContext context)
    {
        await SeedSetsAsync(context);
        var cards = Enumerable.Range(1, 4)
            .Select(index => CreateCard(index, 1, $"term {index}", $"definition {index}", index))
            .ToList();
        context.Flashcards.AddRange(cards);
        await context.SaveChangesAsync();
        return cards;
    }

    private static async Task SeedSetsAsync(AppDbContext context)
    {
        context.FlashcardSets.AddRange(
            new FlashcardSet { Id = 1, Title = "Source", UserId = "user-1" },
            new FlashcardSet { Id = 2, Title = "Other", UserId = "user-1" });
        await context.SaveChangesAsync();
    }

    private static Flashcard CreateCard(
        int id,
        int setId,
        string frontText,
        string backText,
        int orderIndex = 0,
        bool isStarred = false)
    {
        return new Flashcard
        {
            Id = id,
            FlashcardSetId = setId,
            FrontText = frontText,
            BackText = backText,
            Pronunciation = "/test/",
            PartOfSpeech = "noun",
            ExampleSentence = "Example.",
            ExampleMeaning = "Example meaning.",
            OrderIndex = orderIndex,
            IsStarred = isStarred
        };
    }
}
