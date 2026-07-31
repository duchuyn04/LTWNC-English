using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.StudyModes;

public sealed class DictationModeStrategyTests
{
    [Fact]
    public async Task GetCardsAsync_VocabularyMode_IncludesCardsWithoutExampleSentence()
    {
        await using AppDbContext context = await CreateContextAsync();

        List<Flashcard> actual = await CreateStrategy(context).GetCardsAsync(
            1,
            new UserStudySettings { DictationContentMode = DictationContentMode.Vocabulary },
            "user-1");

        Assert.Equal(3, actual.Count);
    }

    [Fact]
    public async Task GetCardsAsync_ExampleSentenceMode_ExcludesBlankExampleSentences()
    {
        await using AppDbContext context = await CreateContextAsync();

        List<Flashcard> actual = await CreateStrategy(context).GetCardsAsync(
            1,
            new UserStudySettings { DictationContentMode = DictationContentMode.ExampleSentence },
            "user-1");

        Flashcard card = Assert.Single(actual);
        Assert.Equal(2, card.Id);
    }

    [Fact]
    public async Task GetCardsAsync_StarredOnly_ReturnsOnlyStarredCards()
    {
        await using AppDbContext context = await CreateContextAsync();

        List<Flashcard> actual = await CreateStrategy(context).GetCardsAsync(
            1,
            new UserStudySettings { StarredOnly = true },
            "user-1");

        Assert.Equal(new[] { 3, 1 }, actual.Select(card => card.Id));
        Assert.All(actual, card => Assert.True(card.IsStarred));
    }

    [Fact]
    public async Task GetCardsAsync_UnlearnedOnly_ReturnsOnlyUnlearnedCardsForCurrentUser()
    {
        await using AppDbContext context = await CreateContextAsync();
        context.UserProgresses.Add(new UserProgress
        {
            UserId = "user-1",
            FlashcardId = 2,
            IsLearned = true
        });
        await context.SaveChangesAsync();

        List<Flashcard> actual = await CreateStrategy(context).GetCardsAsync(
            1,
            new UserStudySettings { UnlearnedOnly = true },
            "user-1");

        Assert.Equal(new[] { 3, 1 }, actual.Select(card => card.Id));
    }

    [Fact]
    public async Task GetCardsAsync_UnfilteredCards_ReturnsCardsOrderedByOrderIndex()
    {
        await using AppDbContext context = await CreateContextAsync();

        List<Flashcard> actual = await CreateStrategy(context).GetCardsAsync(
            1,
            new UserStudySettings(),
            "user-1");

        Assert.Equal(new[] { 2, 3, 1 }, actual.Select(card => card.Id));
    }

    [Fact]
    public void BuildOption_AvailableCards_ReturnsDictationOptionMetadata()
    {
        using AppDbContext context = CreateEmptyContext();
        var cards = new[] { new Flashcard(), new Flashcard() };

        var actual = CreateStrategy(context).BuildOption(7, cards, new UserStudySettings());

        Assert.True(actual.IsAvailable);
        Assert.Equal(StudyMode.Dictation, actual.Mode);
        Assert.Equal(2, actual.CardCount);
        Assert.Equal(50, actual.EstimatedSeconds);
        Assert.Equal("/Study/7/Dictation", actual.ActionUrl);
        Assert.Null(actual.UnavailableReason);
    }

    [Fact]
    public void BuildOption_VocabularyModeWithoutCards_ReturnsGenericUnavailableReason()
    {
        using AppDbContext context = CreateEmptyContext();

        var actual = CreateStrategy(context).BuildOption(
            1,
            Array.Empty<Flashcard>(),
            new UserStudySettings { DictationContentMode = DictationContentMode.Vocabulary });

        Assert.False(actual.IsAvailable);
        Assert.Equal("Không có thẻ phù hợp với bộ lọc hiện tại.", actual.UnavailableReason);
    }

    [Fact]
    public void BuildOption_ExampleSentenceModeWithoutCards_ReturnsExampleSentenceReason()
    {
        using AppDbContext context = CreateEmptyContext();

        var actual = CreateStrategy(context).BuildOption(
            1,
            Array.Empty<Flashcard>(),
            new UserStudySettings { DictationContentMode = DictationContentMode.ExampleSentence });

        Assert.False(actual.IsAvailable);
        Assert.Equal("Không có thẻ có câu ví dụ phù hợp.", actual.UnavailableReason);
    }

    private static DictationModeStrategy CreateStrategy(AppDbContext context) =>
        new(new StudyCardQueryService(context));

    private static AppDbContext CreateEmptyContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<AppDbContext> CreateContextAsync()
    {
        AppDbContext context = CreateEmptyContext();
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = 1,
            UserId = "user-1",
            Title = "Set"
        });
        context.Flashcards.AddRange(
            CreateCard(1, orderIndex: 2, starred: true, example: ""),
            CreateCard(2, orderIndex: 0, starred: false, example: "Example B."),
            CreateCard(3, orderIndex: 1, starred: true, example: "   "));
        await context.SaveChangesAsync();
        return context;
    }

    private static Flashcard CreateCard(int id, int orderIndex, bool starred, string example) => new()
    {
        Id = id,
        FlashcardSetId = 1,
        FrontText = $"term-{id}",
        BackText = $"definition-{id}",
        Pronunciation = "",
        PartOfSpeech = "noun",
        ExampleSentence = example,
        ExampleMeaning = "",
        IsStarred = starred,
        OrderIndex = orderIndex
    };
}
