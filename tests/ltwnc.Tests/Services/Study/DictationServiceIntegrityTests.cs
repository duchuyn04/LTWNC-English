using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Study;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Study;

public sealed class DictationServiceIntegrityTests
{
    [Fact]
    public async Task CheckAnswer_UsesSessionSnapshotAndIsIdempotent()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);
        DictationService service = CreateService(context);
        StudySession session = await service.CreateSessionAsync(
            "user-1",
            1,
            DictationContentMode.Vocabulary,
            cards.Count,
            cards);

        cards[0].FrontText = "changed after session start";
        await context.SaveChangesAsync();

        DictationCheckResult first = await service.CheckAnswerAsync(
            session.Id,
            1,
            cards[0].Id,
            "alpha",
            "user-1",
            acceptSynonyms: false);
        DictationCheckResult duplicate = await service.CheckAnswerAsync(
            session.Id,
            1,
            cards[0].Id,
            "wrong duplicate",
            "user-1",
            acceptSynonyms: false);

        Assert.True(first.IsCorrect);
        Assert.True(duplicate.IsCorrect);
        Assert.Equal("alpha", duplicate.CorrectAnswer);
        Assert.Single(await context.DictationSessionDetails.ToListAsync());
        UserProgress progress = await context.UserProgresses.SingleAsync();
        Assert.Equal(1, progress.CorrectCount);
        Assert.Equal(0, progress.WrongCount);
    }

    [Fact]
    public async Task CheckAnswer_RejectsCardsOutsideSnapshotAndCompletedSessions()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);
        DictationService service = CreateService(context);
        StudySession session = await service.CreateSessionAsync(
            "user-1",
            1,
            DictationContentMode.Vocabulary,
            1,
            cards.Take(1).ToList());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CheckAnswerAsync(
                session.Id,
                1,
                cards[1].Id,
                "beta",
                "user-1",
                acceptSynonyms: false));

        await service.CheckAnswerAsync(
            session.Id,
            1,
            cards[0].Id,
            "alpha",
            "user-1",
            acceptSynonyms: false);
        await service.CompleteSessionAsync(session.Id, 1, "user-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CheckAnswerAsync(
                session.Id,
                1,
                cards[0].Id,
                "alpha",
                "user-1",
                acceptSynonyms: false));
    }

    [Fact]
    public async Task CompleteSession_RequiresEverySnapshottedQuestion()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);
        DictationService service = CreateService(context);
        StudySession session = await service.CreateSessionAsync(
            "user-1",
            1,
            DictationContentMode.Vocabulary,
            cards.Count,
            cards);

        await service.CheckAnswerAsync(
            session.Id,
            1,
            cards[0].Id,
            "alpha",
            "user-1",
            acceptSynonyms: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSessionAsync(session.Id, 1, "user-1"));

        await service.CheckAnswerAsync(
            session.Id,
            1,
            cards[1].Id,
            "wrong",
            "user-1",
            acceptSynonyms: false);
        StudySession completed = await service.CompleteSessionAsync(session.Id, 1, "user-1");

        Assert.Equal(50, completed.Score);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task Result_ValidatesSetModeAndCompletion()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = 2,
            UserId = "user-1",
            Title = "Other set"
        });
        await context.SaveChangesAsync();

        DictationService service = CreateService(context);
        StudySession session = await service.CreateSessionAsync(
            "user-1",
            1,
            DictationContentMode.Vocabulary,
            1,
            cards.Take(1).ToList());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetSessionResultAsync(session.Id, 1, "user-1"));

        await service.CheckAnswerAsync(
            session.Id,
            1,
            cards[0].Id,
            "alpha",
            "user-1",
            acceptSynonyms: false);
        await service.CompleteSessionAsync(session.Id, 1, "user-1");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetSessionResultAsync(session.Id, 2, "user-1"));
    }

    [Fact]
    public async Task RetryAndHistory_UseOnlyWrongSnapshottedQuestions()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);
        DictationService service = CreateService(context);
        StudySession session = await service.CreateSessionAsync(
            "user-1",
            1,
            DictationContentMode.Vocabulary,
            cards.Count,
            cards);

        await service.CheckAnswerAsync(
            session.Id,
            1,
            cards[0].Id,
            "alpha",
            "user-1",
            acceptSynonyms: false);
        await service.CheckAnswerAsync(
            session.Id,
            1,
            cards[1].Id,
            "wrong answer",
            "user-1",
            acceptSynonyms: false);
        await service.CompleteSessionAsync(session.Id, 1, "user-1");

        DictationRetryPlan retry = await service.GetRetryPlanAsync(
            session.Id,
            1,
            "user-1");
        List<DictationHistoryItem> history = await service.GetHistoryAsync(1, "user-1");

        Flashcard retryCard = Assert.Single(retry.Cards);
        Assert.Equal(cards[1].Id, retryCard.Id);
        DictationHistoryItem historyItem = Assert.Single(history);
        Assert.Equal("beta", historyItem.CorrectAnswer);
        Assert.Equal("wrong answer", historyItem.AnsweredText);
    }

    private static DictationService CreateService(AppDbContext context)
    {
        StudyCardQueryService queryService = new StudyCardQueryService(context);
        StudyModeStrategyResolver resolver = new StudyModeStrategyResolver(
            new IStudyModeStrategy[]
            {
                new DictationModeStrategy(queryService)
            });
        return new DictationService(
            context,
            resolver,
            TestStudyEvents.NoOpPublisher());
    }

    private static async Task<List<Flashcard>> SeedSetAndCardsAsync(AppDbContext context)
    {
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = 1,
            UserId = "user-1",
            Title = "Dictation set",
            IsPublic = true
        });
        List<Flashcard> cards =
        [
            new Flashcard
            {
                Id = 1,
                FlashcardSetId = 1,
                FrontText = "alpha",
                BackText = "first",
                ExampleSentence = "Alpha comes first.",
                ExampleMeaning = "Alpha đứng đầu.",
                OrderIndex = 0
            },
            new Flashcard
            {
                Id = 2,
                FlashcardSetId = 1,
                FrontText = "beta",
                BackText = "second",
                ExampleSentence = "Beta comes second.",
                ExampleMeaning = "Beta đứng thứ hai.",
                OrderIndex = 1
            }
        ];
        context.Flashcards.AddRange(cards);
        await context.SaveChangesAsync();
        return cards;
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
